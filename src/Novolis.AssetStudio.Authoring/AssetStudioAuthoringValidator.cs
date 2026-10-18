using Novolis.Visual.Assets;
using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Validates a candidate document before it enters authoring history.
/// </summary>
public static class AssetStudioAuthoringValidator
{
    /// <summary>
    /// Validates semantic graph contracts and the optional module context.
    /// </summary>
    public static AssetStudioDiagnostics Validate(
        AssetStudioDocument document,
        VisualNodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);

        var diagnostics = new List<AssetStudioDiagnostic>();
        diagnostics.AddRange(
            document.Recipe.Validate(registry).Diagnostics.Select(
                diagnostic => AssetStudioDiagnostic.From(
                    diagnostic,
                    document.Revision)));

        var nodes = document.Recipe.Nodes.ToDictionary(node => node.Id);
        var descriptors = document.Recipe.Nodes.ToDictionary(
            node => node.Id,
            node => registry.TryGetDescriptor(node.TypeId, out var descriptor)
                ? descriptor
                : null);
        var inputEndpoints = new HashSet<(NodeId NodeId, PortId PortId)>();

        foreach (var connection in document.Recipe.Connections)
        {
            if (!inputEndpoints.Add(
                    (connection.Target.NodeId, connection.Target.PortId)))
            {
                Add(
                    diagnostics,
                    "AST220",
                    $"Input port '{connection.Target}' has more than one connection.",
                    document.Revision,
                    connectionId: connection.Id);
            }

            if (!TryPort(
                    connection.Source,
                    PortDirection.Output,
                    nodes,
                    descriptors,
                    diagnostics,
                    document.Revision,
                    connection.Id,
                    out var sourcePort) ||
                !TryPort(
                    connection.Target,
                    PortDirection.Input,
                    nodes,
                    descriptors,
                    diagnostics,
                    document.Revision,
                    connection.Id,
                    out var targetPort))
            {
                continue;
            }

            if (!sourcePort!.Contract.IsCompatibleWith(
                    connection.SourceContract) ||
                !targetPort!.Contract.IsCompatibleWith(
                    connection.TargetContract))
            {
                Add(
                    diagnostics,
                    "AST221",
                    "Connection contracts do not match their descriptors.",
                    document.Revision,
                    connectionId: connection.Id);
            }

            if (!connection.SourceContract.IsCompatibleWith(
                    connection.TargetContract))
            {
                Add(
                    diagnostics,
                    "AST222",
                    "Source and target ports have incompatible contracts.",
                    document.Revision,
                    connectionId: connection.Id);
            }
        }

        ValidateAcyclic(document, diagnostics);
        ValidateOutputs(document, nodes, descriptors, diagnostics);
        ValidateParameters(document, descriptors, diagnostics);
        ValidateContext(document, nodes, descriptors, diagnostics);
        return new AssetStudioDiagnostics(diagnostics);
    }

    private static bool TryPort(
        VisualPortReference reference,
        PortDirection direction,
        IReadOnlyDictionary<NodeId, VisualNode> nodes,
        IReadOnlyDictionary<NodeId, VisualNodeDescriptor?> descriptors,
        ICollection<AssetStudioDiagnostic> diagnostics,
        long revision,
        ConnectionId connectionId,
        out VisualPortDescriptor? port)
    {
        port = null;
        if (!nodes.ContainsKey(reference.NodeId) ||
            !descriptors.TryGetValue(reference.NodeId, out var descriptor) ||
            descriptor is null)
        {
            return false;
        }

        port = descriptor.FindPort(reference.PortId);
        if (port is null)
        {
            Add(
                diagnostics,
                "AST223",
                $"Port '{reference.PortId}' is not declared by node '{reference.NodeId}'.",
                revision,
                reference.NodeId,
                connectionId);
            return false;
        }

        if (port.Direction != direction)
        {
            Add(
                diagnostics,
                "AST224",
                $"Port '{reference.PortId}' must be a {direction.ToString().ToLowerInvariant()}.",
                revision,
                reference.NodeId,
                connectionId);
            return false;
        }

        return true;
    }

    private static void ValidateOutputs(
        AssetStudioDocument document,
        IReadOnlyDictionary<NodeId, VisualNode> nodes,
        IReadOnlyDictionary<NodeId, VisualNodeDescriptor?> descriptors,
        ICollection<AssetStudioDiagnostic> diagnostics)
    {
        if ((document.DocumentKind is
                AssetStudioDocumentKind.Material or AssetStudioDocumentKind.Asset) &&
            document.Recipe.Outputs.Length == 0)
        {
            Add(
                diagnostics,
                "AST230",
                "A material or asset document must expose at least one output.",
                document.Revision);
        }

        foreach (var output in document.Recipe.Outputs)
        {
            if (!nodes.ContainsKey(output.Source.NodeId) ||
                !descriptors.TryGetValue(output.Source.NodeId, out var descriptor) ||
                descriptor is null)
            {
                continue;
            }

            var port = descriptor.FindPort(output.Source.PortId);
            if (port is null || port.Direction != PortDirection.Output)
            {
                Add(
                    diagnostics,
                    "AST231",
                    $"Output '{output.Id}' must bind to a declared output port.",
                    document.Revision,
                    output.Source.NodeId);
                continue;
            }

            if (!port.Contract.IsCompatibleWith(output.Contract))
            {
                Add(
                    diagnostics,
                    "AST232",
                    $"Output '{output.Id}' contract does not match its port.",
                    document.Revision,
                    output.Source.NodeId);
            }
        }
    }

    private static void ValidateParameters(
        AssetStudioDocument document,
        IReadOnlyDictionary<NodeId, VisualNodeDescriptor?> descriptors,
        ICollection<AssetStudioDiagnostic> diagnostics)
    {
        foreach (var parameter in document.Recipe.Parameters)
        {
            if (!parameter.Contract.IsValueCompatibleWith(parameter.Value.Contract))
            {
                Add(
                    diagnostics,
                    "AST240",
                    $"Parameter '{parameter.Id}' does not satisfy its contract.",
                    document.Revision);
            }
        }

        if (document.Material is { } material)
        {
            foreach (var parameter in material.Parameters)
            {
                if (parameter.Target is not { } target)
                {
                    continue;
                }

                var node = document.Recipe.Nodes.FirstOrDefault(
                    candidate => candidate.Id == target.NodeId);
                if (node is null)
                {
                    Add(
                        diagnostics,
                        "AST241",
                        $"Material parameter '{parameter.Id}' targets a missing node.",
                        document.Revision,
                        target.NodeId);
                    continue;
                }

                if (!node.Values.ContainsKey(target.ValueName))
                {
                    Add(
                        diagnostics,
                        "AST242",
                        $"Material parameter '{parameter.Id}' targets missing value '{target.ValueName}'.",
                        document.Revision,
                        target.NodeId);
                }
                else if (descriptors.TryGetValue(target.NodeId, out var descriptor) &&
                         descriptor?.Parameters.FirstOrDefault(
                             candidate => candidate.Name == target.ValueName)
                             is { } valueDescriptor &&
                         !parameter.Contract.IsValueCompatibleWith(
                             valueDescriptor.Contract))
                {
                    Add(
                        diagnostics,
                        "AST243",
                        $"Material parameter '{parameter.Id}' does not match its node value contract.",
                        document.Revision,
                        target.NodeId);
                }
            }
        }
    }

    private static void ValidateContext(
        AssetStudioDocument document,
        IReadOnlyDictionary<NodeId, VisualNode> nodes,
        IReadOnlyDictionary<NodeId, VisualNodeDescriptor?> descriptors,
        ICollection<AssetStudioDiagnostic> diagnostics)
    {
        if (document.Asset is not { } asset)
        {
            return;
        }

        foreach (var component in asset.Components)
        {
            if (!nodes.ContainsKey(component.Source.NodeId) ||
                !descriptors.TryGetValue(component.Source.NodeId, out var descriptor) ||
                descriptor?.FindPort(component.Source.PortId) is not { } port ||
                port.Direction != PortDirection.Output)
            {
                Add(
                    diagnostics,
                    "AST250",
                    $"Asset component '{component.Id}' references an invalid output.",
                    document.Revision,
                    component.Source.NodeId);
            }

            if (component.Kind == AssetComponentKind.MaterialBinding &&
                (component.MaterialSlotId is null ||
                 asset.FindSlot(component.MaterialSlotId) is null))
            {
                Add(
                    diagnostics,
                    "AST251",
                    $"Asset component '{component.Id}' references a missing material slot.",
                    document.Revision,
                    component.Source.NodeId);
            }
        }
    }

    private static void ValidateAcyclic(
        AssetStudioDocument document,
        ICollection<AssetStudioDiagnostic> diagnostics)
    {
        var outgoing = document.Recipe.Nodes.ToDictionary(
            node => node.Id,
            _ => new List<NodeId>());
        foreach (var connection in document.Recipe.Connections)
        {
            if (outgoing.TryGetValue(connection.Source.NodeId, out var targets) &&
                outgoing.ContainsKey(connection.Target.NodeId))
            {
                targets.Add(connection.Target.NodeId);
            }
        }

        var state = new Dictionary<NodeId, int>();
        foreach (var node in outgoing.Keys.OrderBy(id => id))
        {
            Visit(node);
        }

        void Visit(NodeId node)
        {
            if (state.TryGetValue(node, out var current))
            {
                if (current == 1)
                {
                    Add(
                        diagnostics,
                        "AST260",
                        "The authoring graph contains a cycle.",
                        document.Revision,
                        node);
                }

                return;
            }

            state[node] = 1;
            foreach (var target in outgoing[node].OrderBy(id => id))
            {
                Visit(target);
            }

            state[node] = 2;
        }
    }

    private static void Add(
        ICollection<AssetStudioDiagnostic> diagnostics,
        string code,
        string message,
        long revision,
        NodeId? nodeId = null,
        ConnectionId? connectionId = null) =>
        diagnostics.Add(
            new AssetStudioDiagnostic(
                AssetStudioDiagnosticSeverity.Error,
                code,
                message,
                revision,
                nodeId: nodeId,
                connectionId: connectionId));
}
