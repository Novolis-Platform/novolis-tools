using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Creates a second node with the same descriptor values.</summary>
public sealed record DuplicateNodeCommand : IAssetStudioCommand
{
    public DuplicateNodeCommand(NodeId sourceNodeId, NodeId duplicateNodeId)
    {
        if (sourceNodeId.IsEmpty)
        {
            throw new ArgumentException(
                "A source node identifier is required.",
                nameof(sourceNodeId));
        }

        if (duplicateNodeId.IsEmpty)
        {
            throw new ArgumentException(
                "A duplicate node identifier is required.",
                nameof(duplicateNodeId));
        }

        SourceNodeId = sourceNodeId;
        DuplicateNodeId = duplicateNodeId;
    }

    public NodeId SourceNodeId { get; }

    public NodeId DuplicateNodeId { get; }

    public string Description => "Duplicate node";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var source = document.Recipe.Nodes.FirstOrDefault(
            node => node.Id == SourceNodeId)
            ?? throw new InvalidOperationException(
                $"Node '{SourceNodeId}' does not exist.");
        var duplicate = VisualNode.Create(
            DuplicateNodeId,
            source.TypeId,
            source.Version,
            source.Values);
        return document.WithRecipe(
            AssetStudioRecipeEditor.AddNode(document.Recipe, duplicate));
    }
}
