using Novolis.Visual.Assets;
using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Host-neutral immutable authoring facade. Every accepted edit is validated
/// before it is placed in the shared session history.
/// </summary>
public sealed class AssetStudioAuthoringSession
{
    private AssetStudioAuthoringSession(
        AssetStudioSessionState state,
        AssetStudioCatalog catalog,
        AssetStudioDiagnostics diagnostics)
    {
        State = state;
        Catalog = catalog;
        Diagnostics = diagnostics;
    }

    /// <summary>Creates an authoring session for a document.</summary>
    public static AssetStudioAuthoringSession Create(
        AssetStudioDocument document,
        AssetStudioCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var composed = catalog ?? new AssetStudioCatalog();
        var state = AssetStudioSessionState.Create(document);
        return new AssetStudioAuthoringSession(
            state,
            composed,
            AssetStudioAuthoringValidator.Validate(
                document,
                composed.Registry));
    }

    /// <summary>The immutable command/history state.</summary>
    public AssetStudioSessionState State { get; }

    /// <summary>The current immutable document.</summary>
    public AssetStudioDocument Document => State.Document;

    /// <summary>The injected palette and module catalog.</summary>
    public AssetStudioCatalog Catalog { get; }

    /// <summary>Diagnostics from the last attempted operation.</summary>
    public AssetStudioDiagnostics Diagnostics { get; }

    /// <summary>Applies and validates one command atomically.</summary>
    public AssetStudioAuthoringResult Apply(IAssetStudioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        AssetStudioDocument candidate;
        try
        {
            candidate = command.Apply(Document);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            KeyNotFoundException)
        {
            return Failure("AST000", exception.Message);
        }

        if (ReferenceEquals(candidate, Document))
        {
            return new AssetStudioAuthoringResult(
                this,
                applied: false,
                AssetStudioDiagnostics.Empty,
                Document.Revision);
        }

        var validation = AssetStudioAuthoringValidator.Validate(
            candidate,
            Catalog.Registry);
        if (validation.HasErrors)
        {
            return new AssetStudioAuthoringResult(
                this,
                applied: false,
                validation,
                Document.Revision);
        }

        var next = new AssetStudioAuthoringSession(
            State.Apply(command),
            Catalog,
            validation);
        return new AssetStudioAuthoringResult(
            next,
            applied: true,
            validation,
            Document.Revision);
    }

    /// <summary>Inserts a descriptor-defined node with its default values.</summary>
    public AssetStudioAuthoringResult InsertNode(
        string typeId,
        NodeId? nodeId = null)
    {
        if (string.IsNullOrWhiteSpace(typeId) ||
            !Catalog.Registry.TryGetDescriptor(typeId, out var descriptor) ||
            descriptor is null)
        {
            return Failure(
                "AST101",
                $"Node type '{typeId}' is not registered in the active catalog.");
        }

        var values = descriptor.Parameters
            .Where(parameter => parameter.DefaultValue is not null)
            .Select(
                parameter => new KeyValuePair<string, VisualValue>(
                    parameter.Name,
                    parameter.DefaultValue!));
        var node = VisualNode.Create(
            nodeId.GetValueOrDefault(NodeId.New()),
            descriptor.TypeId,
            descriptor.Version,
            values);
        return Apply(new AddNodeCommand(node));
    }

    /// <summary>Duplicates one node without copying any connections.</summary>
    public AssetStudioAuthoringResult DuplicateNode(
        NodeId sourceNodeId,
        NodeId? duplicateNodeId = null) =>
        Apply(
            new DuplicateNodeCommand(
                sourceNodeId,
                duplicateNodeId.GetValueOrDefault(NodeId.New())));

    /// <summary>Removes a node and its dependent connections and outputs.</summary>
    public AssetStudioAuthoringResult RemoveNode(NodeId nodeId) =>
        Apply(new RemoveNodeCommand(nodeId));

    /// <summary>Changes one descriptor-declared node value.</summary>
    public AssetStudioAuthoringResult SetNodeValue(
        NodeId nodeId,
        string name,
        VisualValue value) =>
        Apply(new SetNodeValueCommand(nodeId, name, value));

    /// <summary>Connects two typed ports using the active registry contracts.</summary>
    public AssetStudioAuthoringResult Connect(
        NodeId sourceNodeId,
        PortId sourcePortId,
        NodeId targetNodeId,
        PortId targetPortId,
        ConnectionId? connectionId = null)
    {
        try
        {
            return Apply(
                new AddConnectionCommand(
                    CreateConnection(
                        sourceNodeId,
                        sourcePortId,
                        targetNodeId,
                        targetPortId,
                        connectionId.GetValueOrDefault(ConnectionId.New()))));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return Failure("AST102", exception.Message);
        }
    }

    /// <summary>Reconnects an existing edge in one validated operation.</summary>
    public AssetStudioAuthoringResult Reconnect(
        ConnectionId connectionId,
        NodeId sourceNodeId,
        PortId sourcePortId,
        NodeId targetNodeId,
        PortId targetPortId) =>
        Apply(
            new ReconnectConnectionCommand(
                connectionId,
                CreateConnection(
                    sourceNodeId,
                    sourcePortId,
                    targetNodeId,
                    targetPortId,
                    connectionId)));

    /// <summary>Removes one connection.</summary>
    public AssetStudioAuthoringResult Disconnect(ConnectionId connectionId) =>
        Apply(new RemoveConnectionCommand(connectionId));

    /// <summary>Adds an exposed recipe parameter.</summary>
    public AssetStudioAuthoringResult AddParameter(
        VisualParameter parameter) =>
        Apply(new AddParameterCommand(parameter));

    /// <summary>Removes an exposed recipe parameter.</summary>
    public AssetStudioAuthoringResult RemoveParameter(string parameterId) =>
        Apply(new RemoveParameterCommand(parameterId));

    /// <summary>Changes an exposed recipe parameter value.</summary>
    public AssetStudioAuthoringResult SetParameterValue(
        string parameterId,
        VisualValue value) =>
        Apply(new SetParameterValueCommand(parameterId, value));

    /// <summary>Adds an output binding.</summary>
    public AssetStudioAuthoringResult AddOutput(VisualOutput output) =>
        Apply(new AddOutputCommand(output));

    /// <summary>Updates an existing output binding.</summary>
    public AssetStudioAuthoringResult UpdateOutput(VisualOutput output) =>
        Apply(new UpdateOutputCommand(output));

    /// <summary>Removes an output binding.</summary>
    public AssetStudioAuthoringResult RemoveOutput(string outputId) =>
        Apply(new RemoveOutputCommand(outputId));

    /// <summary>Changes one authored material control in place.</summary>
    public AssetStudioAuthoringResult SetMaterialControl(
        string parameterId,
        VisualValue value)
    {
        if (Document.Material?.FindParameter(parameterId) is not { } parameter)
        {
            return Failure(
                "AST110",
                $"Material parameter '{parameterId}' does not exist.");
        }

        if (!parameter.Contract.IsValueCompatibleWith(value.Contract))
        {
            return Failure(
                "AST112",
                $"Material parameter '{parameterId}' has an incompatible value.");
        }

        return Apply(
            new SetMaterialParameterDefaultCommand(parameterId, value));
    }

    /// <summary>Adds one component to an asset assembly.</summary>
    public AssetStudioAuthoringResult AddComponent(
        AssetComponent component) =>
        Apply(new AddAssetComponentCommand(component));

    /// <summary>Removes one component from an asset assembly.</summary>
    public AssetStudioAuthoringResult RemoveComponent(string componentId) =>
        Apply(new RemoveAssetComponentCommand(componentId));

    /// <summary>Adds a material assignment slot to an asset assembly.</summary>
    public AssetStudioAuthoringResult AddMaterialSlot(
        AssetMaterialSlot slot) =>
        Apply(new AddAssetMaterialSlotCommand(slot));

    /// <summary>Changes a material module assignment.</summary>
    public AssetStudioAuthoringResult AssignMaterial(
        string slotId,
        string materialId)
    {
        if (Document.Asset?.FindSlot(slotId) is not { } slot)
        {
            return Failure(
                "AST120",
                $"Material slot '{slotId}' does not exist.");
        }

        try
        {
            return Apply(
                new UpdateAssetMaterialSlotCommand(
                    new AssetMaterialSlot(
                        slot.Id,
                        slot.Name,
                        materialId,
                        slot.Required)));
        }
        catch (ArgumentException exception)
        {
            return Failure("AST121", exception.Message);
        }
    }

    /// <summary>Removes an unused material assignment slot.</summary>
    public AssetStudioAuthoringResult RemoveMaterialSlot(string slotId) =>
        Apply(new RemoveAssetMaterialSlotCommand(slotId));

    /// <summary>Changes an authored asset control and its graph default.</summary>
    public AssetStudioAuthoringResult SetAssetControl(
        string parameterId,
        VisualValue value) =>
        Apply(new SetAssetParameterDefaultCommand(parameterId, value));

    /// <summary>Creates a child instance for the active module.</summary>
    public AssetStudioAuthoringResult CreateInstance(string? instanceId = null)
    {
        if (Document.Material is { } material)
        {
            return Apply(
                new SetMaterialInstanceCommand(
                    new MaterialInstance(
                        instanceId ?? material.MaterialId + ".instance",
                        material)));
        }

        if (Document.Asset is { } asset)
        {
            return Apply(
                new SetAssetInstanceCommand(
                    new AssetInstance(
                        instanceId ?? asset.AssetId + ".instance",
                        asset)));
        }

        return Failure(
            "AST130",
            "Instances require a material or asset document.");
    }

    /// <summary>Sets a child material override.</summary>
    public AssetStudioAuthoringResult SetMaterialInstanceOverride(
        string parameterId,
        VisualValue value) =>
        Apply(new SetMaterialInstanceOverrideCommand(parameterId, value));

    /// <summary>Resets a child material override.</summary>
    public AssetStudioAuthoringResult ResetMaterialInstanceOverride(
        string parameterId) =>
        Apply(new ResetMaterialInstanceOverrideCommand(parameterId));

    /// <summary>Sets a child asset override.</summary>
    public AssetStudioAuthoringResult SetAssetInstanceOverride(
        string parameterId,
        VisualValue value) =>
        Apply(new SetAssetInstanceOverrideCommand(parameterId, value));

    /// <summary>Resets a child asset override.</summary>
    public AssetStudioAuthoringResult ResetAssetInstanceOverride(
        string parameterId) =>
        Apply(new ResetAssetInstanceOverrideCommand(parameterId));

    /// <summary>Undoes one accepted command.</summary>
    public AssetStudioAuthoringResult Undo()
    {
        if (!State.CanUndo)
        {
            return new AssetStudioAuthoringResult(
                this,
                applied: false,
                AssetStudioDiagnostics.Empty,
                Document.Revision);
        }

        var next = new AssetStudioAuthoringSession(
            State.Undo(),
            Catalog,
            AssetStudioDiagnostics.Empty);
        return new AssetStudioAuthoringResult(
            next,
            applied: true,
            AssetStudioDiagnostics.Empty,
            Document.Revision);
    }

    /// <summary>Redoes one previously undone command.</summary>
    public AssetStudioAuthoringResult Redo()
    {
        if (!State.CanRedo)
        {
            return new AssetStudioAuthoringResult(
                this,
                applied: false,
                AssetStudioDiagnostics.Empty,
                Document.Revision);
        }

        var next = new AssetStudioAuthoringSession(
            State.Redo(),
            Catalog,
            AssetStudioDiagnostics.Empty);
        return new AssetStudioAuthoringResult(
            next,
            applied: true,
            AssetStudioDiagnostics.Empty,
            Document.Revision);
    }

    /// <summary>Changes only editor view state, leaving semantic history intact.</summary>
    public AssetStudioAuthoringSession WithView(AssetStudioViewState view) =>
        new(
            State.WithView(view),
            Catalog,
            Diagnostics);

    private VisualConnection CreateConnection(
        NodeId sourceNodeId,
        PortId sourcePortId,
        NodeId targetNodeId,
        PortId targetPortId,
        ConnectionId connectionId)
    {
        var sourcePort = FindPort(
            sourceNodeId,
            sourcePortId,
            PortDirection.Output);
        var targetPort = FindPort(
            targetNodeId,
            targetPortId,
            PortDirection.Input);
        return new VisualConnection(
            connectionId,
            new VisualPortReference(sourceNodeId, sourcePortId),
            new VisualPortReference(targetNodeId, targetPortId),
            sourcePort.Contract,
            targetPort.Contract);
    }

    private VisualPortDescriptor FindPort(
        NodeId nodeId,
        PortId portId,
        PortDirection direction)
    {
        var node = Document.Recipe.Nodes.FirstOrDefault(
            candidate => candidate.Id == nodeId)
            ?? throw new InvalidOperationException(
                $"Node '{nodeId}' does not exist.");
        if (!Catalog.Registry.TryGetDescriptor(node.TypeId, out var descriptor) ||
            descriptor is null)
        {
            throw new InvalidOperationException(
                $"Node type '{node.TypeId}' is not registered.");
        }

        var port = descriptor.FindPort(portId)
            ?? throw new InvalidOperationException(
                $"Port '{portId}' does not exist on node '{nodeId}'.");
        if (port.Direction != direction)
        {
            throw new InvalidOperationException(
                $"Port '{portId}' on node '{nodeId}' is not a {direction.ToString().ToLowerInvariant()}.");
        }

        return port;
    }

    private AssetStudioAuthoringResult Failure(
        string code,
        string message)
    {
        var diagnostics = new AssetStudioDiagnostics(
        [
            new AssetStudioDiagnostic(
                AssetStudioDiagnosticSeverity.Error,
                code,
                message,
                Document.Revision),
        ]);
        return new AssetStudioAuthoringResult(
            new AssetStudioAuthoringSession(State, Catalog, diagnostics),
            applied: false,
            diagnostics,
            Document.Revision);
    }
}
