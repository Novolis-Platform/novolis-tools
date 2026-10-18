using System.Text;
using System.Text.Json;
using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Import;

/// <summary>
/// Imports canonical visual recipe JSON from bounded caller-owned content.
/// </summary>
public static class AssetStudioJsonImporter
{
    /// <summary>
    /// Imports one bounded UTF-8 payload.
    /// </summary>
    public static AssetStudioImportResult Import(
        AssetStudioImportRequest request,
        Guid? documentId = null,
        VisualNodeRegistry? registry = null,
        VisualSchemaMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            request.Limits.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Reject(
                "AST100",
                $"Import limits are invalid: {exception.ParamName}.");
        }

        if (!TryValidatePath(
                request.LogicalPath,
                request.Limits.MaxPathLength,
                out var pathMessage))
        {
            return Reject("AST101", pathMessage);
        }

        if (request.Content.Length > request.Limits.MaxPayloadBytes)
        {
            return Reject(
                "AST102",
                $"Import payload exceeds {request.Limits.MaxPayloadBytes} bytes.");
        }

        try
        {
            using (var document = JsonDocument.Parse(
                       request.Content,
                       new JsonDocumentOptions
                       {
                           MaxDepth = request.Limits.MaxJsonDepth,
                           AllowTrailingCommas = false,
                           CommentHandling = JsonCommentHandling.Disallow,
                       }))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return Reject(
                        "AST103",
                        "The imported recipe must be a JSON object.");
                }
            }

            var recipe = VisualRecipeSerializer.Deserialize(
                Encoding.UTF8.GetString(request.Content.Span),
                registry,
                migrations);
            if (recipe.Nodes.Length > request.Limits.MaxNodes)
            {
                return Reject("AST104", "The imported node count exceeds its limit.");
            }

            if (recipe.Connections.Length > request.Limits.MaxConnections)
            {
                return Reject(
                    "AST105",
                    "The imported connection count exceeds its limit.");
            }

            if (recipe.Parameters.Length > request.Limits.MaxParameters)
            {
                return Reject(
                    "AST106",
                    "The imported parameter count exceeds its limit.");
            }

            if (recipe.Outputs.Length > request.Limits.MaxOutputs)
            {
                return Reject(
                    "AST107",
                    "The imported output count exceeds its limit.");
            }

            return AssetStudioImportResult.Succeed(
                AssetStudioDocument.Create(recipe, documentId));
        }
        catch (JsonException exception)
        {
            return Reject("AST108", $"Invalid recipe JSON: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            return Reject("AST109", $"Recipe validation failed: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return Reject("AST110", $"Recipe validation failed: {exception.Message}");
        }
        catch (OverflowException exception)
        {
            return Reject("AST111", $"Recipe value is out of range: {exception.Message}");
        }
    }

    /// <summary>
    /// Reads a caller-owned stream with a bounded allocation, then imports it.
    /// The stream is not closed by this method.
    /// </summary>
    public static async ValueTask<AssetStudioImportResult> ImportAsync(
        string logicalPath,
        Stream content,
        AssetStudioImportLimits? limits = null,
        Guid? documentId = null,
        VisualNodeRegistry? registry = null,
        VisualSchemaMigrationRegistry? migrations = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var effectiveLimits = limits ?? AssetStudioImportLimits.Default;
        try
        {
            effectiveLimits.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Reject(
                "AST100",
                $"Import limits are invalid: {exception.ParamName}.");
        }

        if (!content.CanRead)
        {
            return Reject("AST112", "The import stream is not readable.");
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var count = await content.ReadAsync(
                    chunk.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > effectiveLimits.MaxPayloadBytes)
            {
                return Reject(
                    "AST102",
                    $"Import payload exceeds {effectiveLimits.MaxPayloadBytes} bytes.");
            }

            await buffer.WriteAsync(
                    chunk.AsMemory(0, count),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Import(
            new AssetStudioImportRequest(
                logicalPath,
                buffer.ToArray(),
                effectiveLimits),
            documentId,
            registry,
            migrations);
    }

    private static bool TryValidatePath(
        string path,
        int maxLength,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            message = "A logical import path is required.";
            return false;
        }

        if (path.Length > maxLength)
        {
            message = $"The logical import path exceeds {maxLength} characters.";
            return false;
        }

        if (path.Contains('\0') ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("\\", StringComparison.Ordinal) ||
            (path.Length >= 2 && path[1] == ':'))
        {
            message = "The logical import path must be relative.";
            return false;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.None);
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Contains(':')))
        {
            message = "The logical import path contains an unsafe segment.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static AssetStudioImportResult Reject(string code, string message) =>
        AssetStudioImportResult.Reject(
        [
            new AssetStudioDiagnostic(
                AssetStudioDiagnosticSeverity.Error,
                code,
                message),
        ]);
}
