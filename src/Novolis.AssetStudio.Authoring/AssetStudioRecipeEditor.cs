using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

internal static class AssetStudioRecipeEditor
{
    public static VisualRecipe AddNode(VisualRecipe recipe, VisualNode node)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(node);
        return With(recipe, nodes: recipe.Nodes.Append(node));
    }

    public static VisualRecipe RemoveNode(VisualRecipe recipe, NodeId nodeId)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var node = RequireNode(recipe, nodeId);
        _ = node;
        return With(
            recipe,
            nodes: recipe.Nodes.Where(candidate => candidate.Id != nodeId),
            connections: recipe.Connections.Where(connection =>
                connection.Source.NodeId != nodeId &&
                connection.Target.NodeId != nodeId),
            outputs: recipe.Outputs.Where(output => output.Source.NodeId != nodeId));
    }

    public static VisualRecipe SetNodeValue(
        VisualRecipe recipe,
        NodeId nodeId,
        string name,
        VisualValue value)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(value);
        var node = RequireNode(recipe, nodeId);
        var replacement = node.WithValue(name, value);
        return With(
            recipe,
            nodes: recipe.Nodes.Select(candidate =>
                candidate.Id == nodeId ? replacement : candidate));
    }

    public static VisualRecipe AddConnection(
        VisualRecipe recipe,
        VisualConnection connection)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(connection);
        return With(recipe, connections: recipe.Connections.Append(connection));
    }

    public static VisualRecipe RemoveConnection(
        VisualRecipe recipe,
        ConnectionId connectionId)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (!recipe.Connections.Any(connection => connection.Id == connectionId))
        {
            throw new InvalidOperationException(
                $"Connection '{connectionId}' does not exist.");
        }

        return With(
            recipe,
            connections: recipe.Connections.Where(connection =>
                connection.Id != connectionId));
    }

    public static VisualRecipe AddParameter(
        VisualRecipe recipe,
        VisualParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(parameter);
        return With(recipe, parameters: recipe.Parameters.Append(parameter));
    }

    public static VisualRecipe RemoveParameter(
        VisualRecipe recipe,
        string parameterId)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        RequireParameter(recipe, parameterId);
        return With(
            recipe,
            parameters: recipe.Parameters.Where(parameter =>
                parameter.Id != parameterId));
    }

    public static VisualRecipe SetParameterValue(
        VisualRecipe recipe,
        string parameterId,
        VisualValue value)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(value);
        var parameter = RequireParameter(recipe, parameterId);
        var replacement = new VisualParameter(
            parameter.Id,
            parameter.Name,
            parameter.Contract,
            value);
        return With(
            recipe,
            parameters: recipe.Parameters.Select(candidate =>
                candidate.Id == parameterId ? replacement : candidate));
    }

    public static VisualRecipe AddOutput(
        VisualRecipe recipe,
        VisualOutput output)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(output);
        return With(recipe, outputs: recipe.Outputs.Append(output));
    }

    public static VisualRecipe RemoveOutput(
        VisualRecipe recipe,
        string outputId)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        RequireOutput(recipe, outputId);
        return With(
            recipe,
            outputs: recipe.Outputs.Where(output => output.Id != outputId));
    }

    public static VisualRecipe SetOutput(
        VisualRecipe recipe,
        VisualOutput output)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(output);
        RequireOutput(recipe, output.Id);
        return With(
            recipe,
            outputs: recipe.Outputs.Select(candidate =>
                candidate.Id == output.Id ? output : candidate));
    }

    private static VisualRecipe With(
        VisualRecipe recipe,
        IEnumerable<VisualNode>? nodes = null,
        IEnumerable<VisualConnection>? connections = null,
        IEnumerable<VisualParameter>? parameters = null,
        IEnumerable<VisualOutput>? outputs = null) =>
        new(
            recipe.RecipeId,
            nodes ?? recipe.Nodes,
            connections ?? recipe.Connections,
            parameters ?? recipe.Parameters,
            outputs ?? recipe.Outputs,
            recipe.SeedPolicy,
            recipe.Dependencies,
            recipe.Capabilities,
            recipe.SchemaVersion);

    private static VisualNode RequireNode(VisualRecipe recipe, NodeId nodeId) =>
        recipe.Nodes.FirstOrDefault(node => node.Id == nodeId) ??
        throw new InvalidOperationException($"Node '{nodeId}' does not exist.");

    private static VisualParameter RequireParameter(
        VisualRecipe recipe,
        string parameterId) =>
        recipe.Parameters.FirstOrDefault(parameter => parameter.Id == parameterId) ??
        throw new InvalidOperationException(
            $"Parameter '{parameterId}' does not exist.");

    private static VisualOutput RequireOutput(
        VisualRecipe recipe,
        string outputId) =>
        recipe.Outputs.FirstOrDefault(output => output.Id == outputId) ??
        throw new InvalidOperationException(
            $"Output '{outputId}' does not exist.");
}
