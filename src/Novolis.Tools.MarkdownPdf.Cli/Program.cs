using System.CommandLine;
using Novolis.Tools.MarkdownPdf;

var root = new RootCommand("novolis-mdpdf — Markdown to PDF with named themes");

var themesCommand = new Command("themes", "List built-in Markdown → PDF themes");
themesCommand.SetAction(_ =>
{
    Console.WriteLine("Id        Display   Description");
    Console.WriteLine("--------- --------- -----------");
    foreach (var theme in MarkdownPdfThemes.All)
    {
        Console.WriteLine($"{theme.Id,-9} {theme.DisplayName,-9} {theme.Description}");
    }

    Console.WriteLine();
    Console.WriteLine($"Default: {MarkdownPdfThemes.DefaultId}");
    return 0;
});

var convertCommand = new Command("convert", "Convert a Markdown file to PDF");
var inOpt = new Option<string>("--in")
{
    Description = "Input .md path",
    Required = true,
};
var outOpt = new Option<string?>("--out")
{
    Description = "Output .pdf path (default: <input>.pdf beside the source, or under ~/.novolis/artifacts/mdpdf)",
};
var themeOpt = new Option<string>("--theme")
{
    Description = "Theme id (see `novolis-mdpdf themes`)",
    DefaultValueFactory = _ => MarkdownPdfThemes.DefaultId,
};
var titleOpt = new Option<string?>("--title") { Description = "Document title override" };
var authorOpt = new Option<string?>("--author") { Description = "Author" };
var coverOpt = new Option<bool>("--cover")
{
    Description = "Emit a first/title page",
    DefaultValueFactory = _ => false,
};
var tocOpt = new Option<bool>("--toc")
{
    Description = "Emit a table of contents from level-1 headings",
    DefaultValueFactory = _ => false,
};

convertCommand.Options.Add(inOpt);
convertCommand.Options.Add(outOpt);
convertCommand.Options.Add(themeOpt);
convertCommand.Options.Add(titleOpt);
convertCommand.Options.Add(authorOpt);
convertCommand.Options.Add(coverOpt);
convertCommand.Options.Add(tocOpt);

convertCommand.SetAction(parseResult =>
{
    var input = Path.GetFullPath(parseResult.GetValue(inOpt)!);
    var themeId = parseResult.GetValue(themeOpt) ?? MarkdownPdfThemes.DefaultId;
    if (!MarkdownPdfThemes.TryGet(themeId, out _))
    {
        Console.Error.WriteLine($"Unknown theme '{themeId}'. Run: novolis-mdpdf themes");
        return 2;
    }

    var output = parseResult.GetValue(outOpt);
    if (string.IsNullOrWhiteSpace(output))
    {
        var artifacts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".novolis",
            "artifacts",
            "mdpdf");
        Directory.CreateDirectory(artifacts);
        output = Path.Combine(artifacts, Path.GetFileNameWithoutExtension(input) + ".pdf");
    }
    else
    {
        output = Path.GetFullPath(output);
    }

    var request = new MarkdownPdfConvertRequest
    {
        Title = parseResult.GetValue(titleOpt),
        Author = parseResult.GetValue(authorOpt),
        IncludeCover = parseResult.GetValue(coverOpt),
        IncludeToc = parseResult.GetValue(tocOpt),
    };

    Console.WriteLine($"Theme:  {themeId}");
    Console.WriteLine($"Input:  {input}");
    Console.WriteLine($"Output: {output}");
    MarkdownPdfConverter.ConvertFile(input, output, themeId, request);
    var bytes = new FileInfo(output).Length;
    Console.WriteLine($"Bytes:  {bytes}");
    return 0;
});

root.Subcommands.Add(themesCommand);
root.Subcommands.Add(convertCommand);

return root.Parse(args).Invoke();
