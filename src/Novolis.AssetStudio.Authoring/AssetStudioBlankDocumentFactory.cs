using Novolis.Visual.Assets;
using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Creates generic, preset-free starter documents for Asset Studio.
/// </summary>
public static class AssetStudioBlankDocumentFactory
{
    private static readonly Guid MaterialOutputNode =
        Guid.Parse("7f0f6e9a-96ef-4ca5-8cf8-000000000001");
    private static readonly Guid AssetGeometryNode =
        Guid.Parse("7f0f6e9a-96ef-4ca5-8cf8-000000000101");

    /// <summary>
    /// Creates a valid material graph with editable, descriptor-defined PBR
    /// defaults. It does not require the preset assembly.
    /// </summary>
    public static MaterialGraph CreateBlankMaterial(
        string materialId = "material",
        string displayName = "Untitled Material",
        AssetStudioCatalog? catalog = null)
    {
        var registry = catalog?.Nodes.Registry
            ?? VisualNodeRegistry.CreateDefault();
        var outputDescriptor = RequireDescriptor(
            registry,
            VisualBuiltInNodeTypes.MaterialOutput);
        var nodes = new List<VisualNode>();
        var connections = new List<VisualConnection>();
        var channels = new List<MaterialChannelBinding>();
        var parameters = new List<MaterialParameterDefinition>();

        var outputId = new NodeId(MaterialOutputNode);
        nodes.Add(
            VisualNode.Create(
                outputId,
                outputDescriptor.TypeId,
                outputDescriptor.Version));

        AddColor(
            "base-color",
            MaterialChannel.BaseColor,
            new ColorValue(0.75, 0.75, 0.78),
            VisualParameterControlKind.Color,
            outputId,
            "baseColor");
        AddScalar(
            "metallic",
            MaterialChannel.Metallic,
            0,
            0,
            1,
            outputId,
            "metallic");
        AddScalar(
            "roughness",
            MaterialChannel.Roughness,
            0.5,
            0,
            1,
            outputId,
            "roughness");
        AddColor(
            "emission-color",
            MaterialChannel.EmissionColor,
            new ColorValue(0, 0, 0),
            VisualParameterControlKind.Color,
            outputId,
            "emissionColor");
        AddScalar(
            "emission-strength",
            MaterialChannel.EmissionStrength,
            0,
            0,
            100,
            outputId,
            "emissionStrength");
        AddScalar(
            "transmission",
            MaterialChannel.Transmission,
            0,
            0,
            1,
            outputId,
            "transmission");
        AddScalar(
            "ior",
            MaterialChannel.Ior,
            1.5,
            1,
            3,
            outputId,
            "ior");
        AddScalar(
            "absorption",
            MaterialChannel.Absorption,
            0,
            0,
            100,
            outputId,
            "absorption");
        AddScalar(
            "clearcoat",
            MaterialChannel.Clearcoat,
            0,
            0,
            1,
            outputId,
            "clearcoat");
        AddScalar(
            "anisotropy",
            MaterialChannel.Anisotropy,
            0,
            -1,
            1,
            outputId,
            "anisotropy");
        AddScalar(
            "alpha",
            MaterialChannel.Alpha,
            1,
            0,
            1,
            outputId,
            "alpha");

        var materialContract = RequirePort(
            outputDescriptor,
            "material",
            PortDirection.Output).Contract;
        var output = new VisualOutput(
            "material",
            "Material",
            new VisualPortReference(outputId, new PortId("material")),
            materialContract);
        var recipe = new VisualRecipe(
            materialId.Trim() + ".graph",
            nodes,
            connections,
            outputs: [output]);
        return new MaterialGraph(
            materialId,
            displayName,
            recipe,
            channels,
            parameters,
            [new MaterialLayer("surface", "Surface", 0)]);

        void AddColor(
            string id,
            MaterialChannel channel,
            ColorValue value,
            VisualParameterControlKind control,
            NodeId targetNode,
            string targetPort)
        {
            var descriptor = RequireDescriptor(
                registry,
                VisualBuiltInNodeTypes.ConstantColor);
            var nodeId = StableNodeId(id);
            var source = RequirePort(
                descriptor,
                "value",
                PortDirection.Output);
            nodes.Add(
                VisualNode.Create(
                    nodeId,
                    descriptor.TypeId,
                    descriptor.Version,
                    [new KeyValuePair<string, VisualValue>("value", value)]));
            AddConnection(nodeId, source, targetNode, targetPort);
            channels.Add(
                new MaterialChannelBinding(
                    channel,
                    new VisualPortReference(nodeId, source.Id),
                    source.Contract));
            parameters.Add(
                new MaterialParameterDefinition(
                    id,
                    DisplayName(id),
                    source.Contract,
                    value,
                    new VisualParameterEditorMetadata(
                        control,
                        DisplayName(id),
                        "Surface"),
                    isInstanceEditable: true,
                    target: new MaterialParameterTarget(nodeId, "value")));
        }

        void AddScalar(
            string id,
            MaterialChannel channel,
            double value,
            double minimum,
            double maximum,
            NodeId targetNode,
            string targetPort)
        {
            var descriptor = RequireDescriptor(
                registry,
                VisualBuiltInNodeTypes.ConstantScalar);
            var nodeId = StableNodeId(id);
            var source = RequirePort(
                descriptor,
                "value",
                PortDirection.Output);
            var scalar = new ScalarValue(value);
            nodes.Add(
                VisualNode.Create(
                    nodeId,
                    descriptor.TypeId,
                    descriptor.Version,
                    [new KeyValuePair<string, VisualValue>("value", scalar)]));
            AddConnection(nodeId, source, targetNode, targetPort);
            channels.Add(
                new MaterialChannelBinding(
                    channel,
                    new VisualPortReference(nodeId, source.Id),
                    source.Contract));
            parameters.Add(
                new MaterialParameterDefinition(
                    id,
                    DisplayName(id),
                    source.Contract,
                    scalar,
                    new VisualParameterEditorMetadata(
                        VisualParameterControlKind.Slider,
                        DisplayName(id),
                        "Surface",
                        minimum: minimum,
                        maximum: maximum,
                        step: 0.01),
                    isInstanceEditable: true,
                    target: new MaterialParameterTarget(nodeId, "value")));
        }

        void AddConnection(
            NodeId sourceNode,
            VisualPortDescriptor source,
            NodeId targetNode,
            string targetPort)
        {
            var target = RequirePort(
                outputDescriptor,
                targetPort,
                PortDirection.Input);
            var connectionIndex = connections.Count + 1;
            connections.Add(
                new VisualConnection(
                    new ConnectionId(
                        Guid.Parse(
                            $"7f0f6e9a-96ef-4ca5-8cf8-{connectionIndex:D12}")),
                    new VisualPortReference(sourceNode, source.Id),
                    new VisualPortReference(targetNode, target.Id),
                    source.Contract,
                    target.Contract));
        }
    }

    /// <summary>
    /// Creates a generic asset assembly with one editable geometry component
    /// and a material slot. The slot deliberately references a neutral module
    /// id rather than a product preset.
    /// </summary>
    public static AssetRecipe CreateBlankAsset(
        string assetId = "asset",
        string displayName = "Untitled Asset",
        AssetStudioCatalog? catalog = null)
    {
        var registry = catalog?.Nodes.Registry
            ?? VisualNodeRegistry.CreateDefault();
        var descriptor = RequireDescriptor(
            registry,
            VisualBuiltInNodeTypes.ConstantGeometry);
        var geometryPort = RequirePort(
            descriptor,
            "value",
            PortDirection.Output);
        var nodeId = new NodeId(AssetGeometryNode);
        var source = new VisualPortReference(nodeId, geometryPort.Id);
        var geometry = new AssetGeometryDefinition(
            "geometry.default",
            AssetGeometryPrimitive.Box);
        var node = VisualNode.Create(
            nodeId,
            descriptor.TypeId,
            descriptor.Version,
            [
                new KeyValuePair<string, VisualValue>(
                    "value",
                    new GeometryValue(
                        "geometry.default",
                        GeometryKind.Mesh,
                        CoordinateSpace.Object,
                        VisualUnit.Meters)),
            ]);
        var recipe = new VisualRecipe(
            assetId.Trim() + ".graph",
            [node],
            outputs:
            [
                new VisualOutput(
                    "geometry",
                    "Geometry",
                    source,
                    geometryPort.Contract),
            ]);
        return new AssetRecipe(
            assetId,
            displayName,
            recipe,
            [
                new AssetComponent(
                    "geometry",
                    "Geometry",
                    AssetComponentKind.Geometry,
                    source,
                    geometry: geometry),
                new AssetComponent(
                    "geometry.material",
                    "Geometry material",
                    AssetComponentKind.MaterialBinding,
                    source,
                    materialSlotId: "surface"),
            ],
            [new AssetMaterialSlot("surface", "Surface", "material.default")]);
    }

    /// <summary>Creates a blank material document.</summary>
    public static AssetStudioDocument CreateMaterialDocument(
        string materialId = "material",
        string displayName = "Untitled Material",
        AssetStudioCatalog? catalog = null,
        Guid? documentId = null) =>
        AssetStudioDocument.CreateMaterial(
            CreateBlankMaterial(materialId, displayName, catalog),
            documentId);

    /// <summary>Creates a blank asset document.</summary>
    public static AssetStudioDocument CreateAssetDocument(
        string assetId = "asset",
        string displayName = "Untitled Asset",
        AssetStudioCatalog? catalog = null,
        Guid? documentId = null) =>
        AssetStudioDocument.CreateAsset(
            CreateBlankAsset(assetId, displayName, catalog),
            documentId);

    private static VisualNodeDescriptor RequireDescriptor(
        VisualNodeRegistry registry,
        string typeId) =>
        registry.TryGetDescriptor(typeId, out var descriptor) &&
        descriptor is not null
            ? descriptor
            : throw new InvalidOperationException(
                $"Node descriptor '{typeId}' is not registered.");

    private static VisualPortDescriptor RequirePort(
        VisualNodeDescriptor descriptor,
        string portId,
        PortDirection direction) =>
        descriptor.Ports.FirstOrDefault(
            port => port.Id == new PortId(portId) &&
                    port.Direction == direction)
        ?? throw new InvalidOperationException(
            $"Port '{portId}' is not available on '{descriptor.TypeId}'.");

    private static NodeId StableNodeId(string name)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("assetstudio.blank.material|" + name));
        return new NodeId(new Guid(bytes.AsSpan(0, 16)));
    }

    private static string DisplayName(string id) =>
        string.Join(
            ' ',
            id.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
