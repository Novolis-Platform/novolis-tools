using System.Collections.Immutable;
using System.Security.Cryptography;
using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Definitions;
using SkiaSharp;

namespace Novolis.AssetStudio.Import;

/// <summary>
/// Decodes bounded PNG, JPEG, and WebP inputs into deterministic RGBA assets.
/// No source path or decoder object is retained in the result.
/// </summary>
public static class AssetStudioImageImporter
{
    private static readonly ImmutableHashSet<string> SupportedExtensions =
        ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            ".png",
            ".jpg",
            ".jpeg",
            ".webp");

    public static AssetStudioImageImportResult Import(
        AssetStudioImageImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            request.Limits.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Reject(
                "ASI100",
                $"Image import limits are invalid: {exception.ParamName}.");
        }

        if (!TryValidatePath(request.LogicalPath, request.Limits, out var pathMessage))
        {
            return Reject("ASI101", pathMessage);
        }

        if (request.Content.Length <= 0)
        {
            return Reject("ASI102", "The image payload is empty.");
        }

        if (request.Content.Length > request.Limits.MaxPayloadBytes)
        {
            return Reject(
                "ASI103",
                $"Image payload exceeds {request.Limits.MaxPayloadBytes} bytes.");
        }

        var sourceFormat = DetectFormat(request.Content.Span);
        if (sourceFormat == VisualImageSourceFormat.Unknown)
        {
            return Reject(
                "ASI104",
                "The image format is not PNG, JPEG, or WebP.");
        }

        try
        {
            using var data = SKData.CreateCopy(request.Content.ToArray());
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                return Reject("ASI105", "The image header could not be identified.");
            }

            if (!IsWithinLimits(
                    codec.Info.Width,
                    codec.Info.Height,
                    request.Limits,
                    out var sizeMessage))
            {
                return Reject("ASI106", sizeMessage);
            }

            var frameCount = Math.Max(codec.FrameCount, 1);
            if (frameCount > request.Limits.MaxFrames)
            {
                return Reject(
                    "ASI107",
                    $"The image contains more than {request.Limits.MaxFrames} frames.");
            }

            var decodedPixels = checked(
                (long)codec.Info.Width * codec.Info.Height * frameCount);
            if (decodedPixels > request.Limits.MaxDecodedPixels)
            {
                return Reject(
                    "ASI106",
                    $"Decoded animation pixels exceed {request.Limits.MaxDecodedPixels}.");
            }

            var asset = CreateAsset(
                request,
                sourceFormat,
                codec,
                frameCount);
            return AssetStudioImageImportResult.Succeed(asset);
        }
        catch (Exception exception) when (
            exception is InvalidDataException ||
            exception is ArgumentException ||
            exception is NotSupportedException)
        {
            return Reject("ASI108", $"Image decoding failed: {exception.Message}");
        }
    }

    public static async ValueTask<AssetStudioImageImportResult> ImportAsync(
        string logicalPath,
        Stream content,
        string? assetId = null,
        ColorSpace colorSpace = ColorSpace.Srgb,
        AlphaMode alphaMode = AlphaMode.Straight,
        AssetStudioImageImportLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var effectiveLimits = limits ?? AssetStudioImageImportLimits.Default;
        try
        {
            effectiveLimits.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Reject(
                "ASI100",
                $"Image import limits are invalid: {exception.ParamName}.");
        }

        if (!content.CanRead)
        {
            return Reject("ASI109", "The image stream is not readable.");
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
                    "ASI103",
                    $"Image payload exceeds {effectiveLimits.MaxPayloadBytes} bytes.");
            }

            await buffer.WriteAsync(
                    chunk.AsMemory(0, count),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Import(
            new AssetStudioImageImportRequest(
                logicalPath,
                buffer.ToArray(),
                assetId,
                colorSpace,
                alphaMode,
                effectiveLimits));
    }

    private static VisualImageAsset CreateAsset(
        AssetStudioImageImportRequest request,
        VisualImageSourceFormat sourceFormat,
        SKCodec codec,
        int frameCount)
    {
        var frames = new List<(byte[] Pixels, int Width, int Height)>();
        var atlasWidth = 0;
        var atlasHeight = 0;
        var width = codec.Info.Width;
        var height = codec.Info.Height;
        var imageInfo = new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            using var bitmap = new SKBitmap(imageInfo);
            var result = codec.GetPixels(
                imageInfo,
                bitmap.GetPixels(),
                new SKCodecOptions(frameIndex));
            if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
            {
                throw new InvalidDataException(
                    $"Frame {frameIndex} could not be decoded ({result}).");
            }

            var colors = bitmap.Pixels;
            var bytes = new byte[checked(width * height * 4)];
            for (var pixelIndex = 0; pixelIndex < colors.Length; pixelIndex++)
            {
                var color = colors[pixelIndex];
                var offset = pixelIndex * 4;
                bytes[offset] = color.Red;
                bytes[offset + 1] = color.Green;
                bytes[offset + 2] = color.Blue;
                bytes[offset + 3] = color.Alpha;
            }

            frames.Add((bytes, width, height));
            atlasWidth = Math.Max(atlasWidth, width);
            atlasHeight = checked(atlasHeight + height);
        }

        var atlasPixels = new byte[checked(atlasWidth * atlasHeight * 4)];
        var spriteFrames = new List<VisualSpriteFrame>(frames.Count);
        var y = 0;
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            for (var row = 0; row < frame.Height; row++)
            {
                Buffer.BlockCopy(
                    frame.Pixels,
                    row * frame.Width * 4,
                    atlasPixels,
                    (y + row) * atlasWidth * 4,
                    frame.Width * 4);
            }

            spriteFrames.Add(
                new VisualSpriteFrame(
                    $"frame-{index:D4}",
                    0,
                    y,
                    frame.Width,
                    frame.Height));
            y += frame.Height;
        }

        return new VisualImageAsset(
            request.AssetId,
            atlasWidth,
            atlasHeight,
            sourceFormat,
            request.ColorSpace,
            request.AlphaMode,
            atlasPixels,
            spriteFrames,
            Convert.ToHexString(
                    SHA256.HashData(request.Content.Span))
                .ToLowerInvariant());
    }

    private static bool IsWithinLimits(
        int width,
        int height,
        AssetStudioImageImportLimits limits,
        out string message)
    {
        if (width <= 0 ||
            height <= 0 ||
            width > limits.MaxDimension ||
            height > limits.MaxDimension)
        {
            message =
                $"Image dimensions must be between 1 and {limits.MaxDimension}.";
            return false;
        }

        if ((long)width * height > limits.MaxDecodedPixels)
        {
            message =
                $"Decoded image pixels exceed {limits.MaxDecodedPixels}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static VisualImageSourceFormat DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[..8].SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return VisualImageSourceFormat.Png;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xd8)
        {
            return VisualImageSourceFormat.Jpeg;
        }

        if (bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return VisualImageSourceFormat.Webp;
        }

        return VisualImageSourceFormat.Unknown;
    }

    private static bool TryValidatePath(
        string path,
        AssetStudioImageImportLimits limits,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            message = "A logical image path is required.";
            return false;
        }

        if (path.Length > limits.MaxPathLength)
        {
            message = $"The logical image path exceeds {limits.MaxPathLength} characters.";
            return false;
        }

        if (path.Contains('\0') ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("\\", StringComparison.Ordinal) ||
            (path.Length >= 2 && path[1] == ':'))
        {
            message = "The logical image path must be relative.";
            return false;
        }

        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Contains(':')))
        {
            message = "The logical image path contains an unsafe segment.";
            return false;
        }

        if (!SupportedExtensions.Contains(Path.GetExtension(normalized)))
        {
            message = "Only PNG, JPEG, and WebP image paths are accepted.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static AssetStudioImageImportResult Reject(
        string code,
        string message) =>
        AssetStudioImageImportResult.Reject(
        [
            new AssetStudioDiagnostic(
                AssetStudioDiagnosticSeverity.Error,
                code,
                message),
        ]);
}
