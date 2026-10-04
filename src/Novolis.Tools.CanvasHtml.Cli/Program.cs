using System.CommandLine;
using Novolis.Tools.CanvasHtml;

var pathArgument = new Argument<string>("path")
{
    Description = "A .canvas.tsx file or a directory of them.",
};

var outOption = new Option<string?>("--out")
{
    Description = "Directory for the HTML files. Defaults to each source file's directory.",
};

var combineOption = new Option<string?>("--combine")
{
    Description = "Write one tabbed HTML file instead of one file per canvas.",
};

var root = new RootCommand(
    "novolis-canvas-html — render Cursor .canvas.tsx files to self-contained HTML.")
{
    pathArgument,
    outOption,
    combineOption,
};

root.SetAction(parseResult =>
{
    var path = parseResult.GetValue(pathArgument);
    if (string.IsNullOrWhiteSpace(path))
    {
        Console.Error.WriteLine("Provide a .canvas.tsx file or a directory.");
        return 1;
    }

    var outDir = parseResult.GetValue(outOption);
    var combine = parseResult.GetValue(combineOption);
    var files = Resolve(path);
    if (files.Count == 0)
    {
        Console.Error.WriteLine("No .canvas.tsx files at " + path);
        return 1;
    }

    if (!string.IsNullOrWhiteSpace(combine))
        return Combine(files, combine);

    var failures = 0;
    foreach (var file in files)
    {
        try
        {
            var html = CanvasHtmlRenderer.RenderFile(file);
            var destinationDirectory = string.IsNullOrWhiteSpace(outDir)
                ? Path.GetDirectoryName(file) ?? "."
                : outDir;
            Directory.CreateDirectory(destinationDirectory);
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith(".canvas", StringComparison.OrdinalIgnoreCase))
                name = name[..^".canvas".Length];
            var destination = Path.Combine(destinationDirectory, name + ".html");
            File.WriteAllText(destination, html);
            Console.WriteLine(destination);
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine(file);
            Console.Error.WriteLine(ex.Message);
        }
    }

    return failures == 0 ? 0 : 1;
});

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);

static int Combine(IReadOnlyList<string> files, string destination)
{
    var pages = new List<CanvasPage>();
    var failures = 0;
    foreach (var file in files)
    {
        try
        {
            pages.Add(new CanvasPage(TitleFrom(file), CanvasHtmlRenderer.RenderBody(File.ReadAllText(file))));
        }
        catch (Exception ex)
        {
            failures++;
            Console.Error.WriteLine(file);
            Console.Error.WriteLine(ex.Message);
        }
    }

    if (pages.Count == 0)
        return 1;

    var title = Path.GetFileNameWithoutExtension(destination);
    var html = CanvasHtmlRenderer.Combine(pages, title);
    var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
    if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
    File.WriteAllText(destination, html);
    Console.WriteLine(Path.GetFullPath(destination));
    return failures == 0 ? 0 : 1;
}

static string TitleFrom(string path)
{
    var name = Path.GetFileNameWithoutExtension(path);
    if (name.EndsWith(".canvas", StringComparison.OrdinalIgnoreCase))
        name = name[..^".canvas".Length];
    return name.Replace('-', ' ');
}

static IReadOnlyList<string> Resolve(string path)
{
    if (File.Exists(path))
        return [Path.GetFullPath(path)];

    if (!Directory.Exists(path))
        return [];

    return Directory
        .EnumerateFiles(path, "*.canvas.tsx", SearchOption.AllDirectories)
        .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
