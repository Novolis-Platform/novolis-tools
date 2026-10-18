using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Descriptor-driven field shown by a generic node or exposed-control
/// inspector.
/// </summary>
public sealed record AssetStudioInspectorField(
    string Name,
    string DisplayName,
    VisualTypeContract Contract,
    VisualValue? Value,
    VisualParameterEditorMetadata Editor,
    bool IsExposed,
    string? Description);
