using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// A deterministic diagnostic surfaced by an Asset Studio headless service.
/// </summary>
public sealed record AssetStudioDiagnostic
{
    /// <summary>
    /// Creates a diagnostic.
    /// </summary>
    public AssetStudioDiagnostic(
        AssetStudioDiagnosticSeverity severity,
        string code,
        string message,
        long? documentRevision = null,
        string? sourcePath = null,
        NodeId? nodeId = null,
        ConnectionId? connectionId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A diagnostic code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "A diagnostic message is required.",
                nameof(message));
        }

        if (documentRevision is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentRevision));
        }

        Severity = severity;
        Code = code;
        Message = message;
        DocumentRevision = documentRevision;
        SourcePath = sourcePath;
        NodeId = nodeId;
        ConnectionId = connectionId;
    }

    /// <summary>
    /// Diagnostic severity.
    /// </summary>
    public AssetStudioDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Stable diagnostic code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Document revision that produced the diagnostic.
    /// </summary>
    public long? DocumentRevision { get; }

    /// <summary>
    /// Optional logical source path.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    /// Optional related node.
    /// </summary>
    public NodeId? NodeId { get; }

    /// <summary>
    /// Optional related connection.
    /// </summary>
    public ConnectionId? ConnectionId { get; }

    /// <summary>
    /// Maps a visual schema diagnostic.
    /// </summary>
    public static AssetStudioDiagnostic From(
        VisualDiagnostic diagnostic,
        long? documentRevision = null) =>
        new(
            diagnostic.Severity == VisualDiagnosticSeverity.Error
                ? AssetStudioDiagnosticSeverity.Error
                : diagnostic.Severity == VisualDiagnosticSeverity.Warning
                    ? AssetStudioDiagnosticSeverity.Warning
                    : AssetStudioDiagnosticSeverity.Info,
            diagnostic.Code,
            diagnostic.Message,
            documentRevision,
            nodeId: diagnostic.NodeId,
            connectionId: diagnostic.ConnectionId);

    /// <summary>
    /// Maps a compiler diagnostic.
    /// </summary>
    public static AssetStudioDiagnostic From(
        VisualCompileDiagnostic diagnostic,
        long? documentRevision = null) =>
        new(
            diagnostic.Severity == VisualCompileDiagnosticSeverity.Error
                ? AssetStudioDiagnosticSeverity.Error
                : diagnostic.Severity == VisualCompileDiagnosticSeverity.Warning
                    ? AssetStudioDiagnosticSeverity.Warning
                    : AssetStudioDiagnosticSeverity.Info,
            diagnostic.Code,
            diagnostic.Message,
            documentRevision,
            diagnostic.SourcePath,
            diagnostic.NodeId,
            diagnostic.ConnectionId);
}
