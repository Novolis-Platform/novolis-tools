using System.CommandLine;
using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Site;

var root = new RootCommand("novolis-docs — Markdown + Mermaid documentation, relationship graphs, and docs/ HTML sites");

var scaffoldCommand = new Command("scaffold", "Write a starter Markdown doc pack with Mermaid diagrams");
var scaffoldTitle = new Option<string>("--title")
{
    Description = "Documentation pack title",
    Required = true,
};
var scaffoldOut = new Option<string>("--out")
{
    Description = "Output directory for *.md files",
    Required = true,
};
var scaffoldSummary = new Option<string?>("--summary")
{
    Description = "Optional one-line summary for overview.md",
};
scaffoldCommand.Options.Add(scaffoldTitle);
scaffoldCommand.Options.Add(scaffoldOut);
scaffoldCommand.Options.Add(scaffoldSummary);
scaffoldCommand.SetAction(parseResult =>
{
    var title = parseResult.GetValue(scaffoldTitle)!;
    var output = parseResult.GetValue(scaffoldOut)!;
    var summary = parseResult.GetValue(scaffoldSummary);
    var written = DocPackBuilder.Scaffold(title, summary).WriteTo(output);
    Console.WriteLine($"Wrote {written.Count} Markdown files to {Path.GetFullPath(output)}");
    foreach (var path in written)
    {
        Console.WriteLine($"  {path}");
    }

    return 0;
});

var graphCommand = new Command("graph", "Scan a .csproj tree and emit a Markdown + Mermaid relationship pack");
var graphRoot = new Option<string>("--root")
{
    Description = "Directory to scan for *.csproj",
    Required = true,
};
var graphOut = new Option<string>("--out")
{
    Description = "Output directory for *.md files",
    Required = true,
};
var graphTitle = new Option<string?>("--title")
{
    Description = "Pack title (defaults to directory name)",
};
var graphIncludePackages = new Option<bool>("--include-packages")
{
    Description = "Include PackageReference edges",
    DefaultValueFactory = _ => true,
};
var graphNovolisOnly = new Option<bool>("--novolis-packages-only")
{
    Description = "When including packages, keep only Novolis.* identities",
    DefaultValueFactory = _ => false,
};
graphCommand.Options.Add(graphRoot);
graphCommand.Options.Add(graphOut);
graphCommand.Options.Add(graphTitle);
graphCommand.Options.Add(graphIncludePackages);
graphCommand.Options.Add(graphNovolisOnly);
graphCommand.SetAction(parseResult =>
{
    var scanRoot = parseResult.GetValue(graphRoot)!;
    var output = parseResult.GetValue(graphOut)!;
    var title = parseResult.GetValue(graphTitle) ?? new DirectoryInfo(Path.GetFullPath(scanRoot)).Name;
    var includePackages = parseResult.GetValue(graphIncludePackages);
    var novolisOnly = parseResult.GetValue(graphNovolisOnly);

    var graph = ProjectGraphScanner.Scan(scanRoot, includePackages, novolisOnly);
    var written = DocPackBuilder.FromGraph(title, graph).WriteTo(output);
    Console.WriteLine($"Scanned {graph.Nodes.Count} nodes / {graph.Edges.Count} edges");
    Console.WriteLine($"Wrote {written.Count} Markdown files to {Path.GetFullPath(output)}");
    foreach (var path in written)
    {
        Console.WriteLine($"  {path}");
    }

    return 0;
});

var siteCommand = new Command("site", "Build a static HTML docs site from a sparse-checkout corpus of {repo}/docs/**/*.md");
var siteCorpus = new Option<string>("--corpus")
{
    Description = "Corpus root containing {repo}/docs/**/*.md",
    Required = true,
};
var siteOut = new Option<string>("--out")
{
    Description = "Output directory for index.html and docs/*.html",
    Required = true,
};
var siteOrg = new Option<string>("--org")
{
    Description = "GitHub organization for source links",
    DefaultValueFactory = _ => "Novolis-Platform",
};
var siteAssets = new Option<string?>("--assets")
{
    Description = "Optional directory with site.css / site.js",
};
var siteBrand = new Option<string?>("--brand")
{
    Description = "Optional brand directory (favicon, logos, banners)",
};
var siteCatalog = new Option<string?>("--catalog")
{
    Description = "Optional repo-catalog.json (tag, blurb, topics) for cards and docs headers",
};
var siteBranch = new Option<string>("--branch")
{
    Description = "Default git branch for GitHub blob URLs",
    DefaultValueFactory = _ => "main",
};
var siteBaseUrl = new Option<string?>("--base-url")
{
    Description = "Public site base URL",
};
siteCommand.Options.Add(siteCorpus);
siteCommand.Options.Add(siteOut);
siteCommand.Options.Add(siteOrg);
siteCommand.Options.Add(siteAssets);
siteCommand.Options.Add(siteBrand);
siteCommand.Options.Add(siteCatalog);
siteCommand.Options.Add(siteBranch);
siteCommand.Options.Add(siteBaseUrl);
siteCommand.SetAction(parseResult =>
{
    var options = new DocsSiteOptions
    {
        CorpusDirectory = parseResult.GetValue(siteCorpus)!,
        OutputDirectory = parseResult.GetValue(siteOut)!,
        Org = parseResult.GetValue(siteOrg)!,
        AssetsDirectory = parseResult.GetValue(siteAssets),
        BrandDirectory = parseResult.GetValue(siteBrand),
        CatalogPath = parseResult.GetValue(siteCatalog),
        DefaultBranch = parseResult.GetValue(siteBranch)!,
        BaseUrl = parseResult.GetValue(siteBaseUrl),
    };
    var count = DocsSiteBuilder.Build(options);
    Console.WriteLine($"Built docs site with {count} pages at {Path.GetFullPath(options.OutputDirectory)}");
    return 0;
});

root.Subcommands.Add(scaffoldCommand);
root.Subcommands.Add(graphCommand);
root.Subcommands.Add(siteCommand);

return root.Parse(args).Invoke();
