using System.CommandLine;

namespace Novolis.Tools.Doom.Cli;

internal static class DoomCommandLine
{
    public static RootCommand CreateRootCommand()
    {
        var input = new Option<string>("--input")
        {
            Description = "Path to the IWAD or base PWAD.",
            Required = true,
        };
        var patch = new Option<string[]>("--patch")
        {
            Description = "Optional PWAD overlay. Repeat for multiple overlays.",
        };
        var map = new Option<string>("--map")
        {
            Description = "Map marker to extract.",
            DefaultValueFactory = _ => "E1M1",
        };
        var output = new Option<string>("--output")
        {
            Description = "Directory for the generated level package.",
            Required = true,
        };
        var profile = new Option<string>("--content-profile")
        {
            Description = "Content profile recorded in the manifest: freedoom or original-local.",
            DefaultValueFactory = _ => "original-local",
        };

        var extract = new Command("extract", "Extract a classic Doom map and its referenced resources.")
        {
            input,
            patch,
            map,
            output,
            profile,
        };
        extract.SetAction(async (parseResult, _) =>
        {
            var manifest = DoomResourceExtractor.Extract(new DoomExtractionOptions
            {
                BaseWadPath = parseResult.GetValue(input)!,
                PatchWadPaths = parseResult.GetValue(patch) ?? [],
                MapName = parseResult.GetValue(map) ?? "E1M1",
                OutputDirectory = parseResult.GetValue(output)!,
                ContentProfile = parseResult.GetValue(profile) ?? "original-local",
            });
            Console.WriteLine($"Extracted {manifest.MapName} with {manifest.Assets.Length} assets.");
            return await Task.FromResult(0);
        });

        return new RootCommand("novolis-doom — extract classic Doom WAD maps and resources")
        {
            extract,
        };
    }
}
