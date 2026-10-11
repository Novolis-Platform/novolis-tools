namespace Novolis.Tools.Doom;

public static class DoomTextureComposer
{
    public static DoomRgbaImage Compose(
        DoomTextureDefinition definition,
        IReadOnlyList<string> patchNames,
        IReadOnlyDictionary<string, byte[]> patchData,
        DoomPalette palette)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(patchNames);
        ArgumentNullException.ThrowIfNull(patchData);
        ArgumentNullException.ThrowIfNull(palette);

        var pixels = new byte[checked(definition.Width * definition.Height * 4)];
        foreach (var placement in definition.Patches)
        {
            if (placement.PatchNameIndex < 0 || placement.PatchNameIndex >= patchNames.Count)
            {
                continue;
            }

            var patchName = patchNames[placement.PatchNameIndex];
            if (!patchData.TryGetValue(patchName, out var patchBytes))
            {
                continue;
            }

            var patch = DoomPatchDecoder.Decode(patchBytes, palette);
            for (var sourceY = 0; sourceY < patch.Height; sourceY++)
            {
                var targetY = placement.OriginY + sourceY;
                if (targetY < 0 || targetY >= definition.Height)
                {
                    continue;
                }

                for (var sourceX = 0; sourceX < patch.Width; sourceX++)
                {
                    var targetX = placement.OriginX + sourceX;
                    if (targetX < 0 || targetX >= definition.Width)
                    {
                        continue;
                    }

                    var source = (sourceY * patch.Width + sourceX) * 4;
                    if (patch.Pixels[source + 3] == 0)
                    {
                        continue;
                    }

                    var target = (targetY * definition.Width + targetX) * 4;
                    patch.Pixels.AsSpan(source, 4).CopyTo(pixels.AsSpan(target, 4));
                }
            }
        }

        return new DoomRgbaImage(definition.Width, definition.Height, pixels);
    }
}
