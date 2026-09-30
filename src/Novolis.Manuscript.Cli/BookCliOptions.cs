using System.Text;
using System.Text.Json;
using Novolis.IO.Paths;
using Novolis.Manuscript;
using Novolis.Manuscript.Editorial;
using Novolis.Manuscript.IO;
using Novolis.Manuscript.Metrics;

namespace Novolis.Manuscript.Cli;

sealed class BookCliOptions
{
    public string? StartDir { get; init; }
    public string? BookFile { get; init; }
    public string? ChaptersDir { get; init; }
    public string? Series { get; init; }
    public string? Book { get; init; }
    public string? Title { get; init; }
    public string? Character { get; init; }
    public string? OutPath { get; init; }
    public double? After { get; init; }
    public double? Key { get; init; }
    public double? From { get; init; }
    public double? To { get; init; }
    public bool Apply { get; init; }
    public bool DryRun { get; init; }
    public bool Relax { get; init; }
    public bool Json { get; init; }
    public EditorialProfile EditorialProfile { get; init; } = EditorialProfile.Fiction;
    public bool? EnableLexicon { get; init; }
    public bool EnableSlop { get; init; } = true;
    public bool EnableNaming { get; init; } = true;
    public IReadOnlyList<string> Paths { get; init; } = [];

    public static BookCliOptions Parse(string[] args)
    {
        string? start = null, bookFile = null, chapters = null, series = null, book = null, title = null;
        string? character = null, outPath = null;
        double? after = null, key = null, from = null, to = null;
        var apply = false;
        var dry = false;
        var relax = false;
        var json = false;
        var profile = EditorialProfile.Fiction;
        bool? enableLexicon = null;
        var enableSlop = true;
        var enableNaming = true;
        var paths = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Need() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException($"Missing value for {a}");
            switch (a)
            {
                case "--root":
                case "--workspace":
                    start = Need();
                    break;
                case "-b":
                case "--book-file":
                    bookFile = Need();
                    break;
                case "--chapters-dir":
                    chapters = Need();
                    break;
                case "--series":
                    series = Need();
                    break;
                case "--book":
                    book = Need();
                    break;
                case "--title":
                    title = Need();
                    break;
                case "--character":
                    character = Need();
                    break;
                case "-o":
                case "--out":
                    outPath = Need();
                    break;
                case "--after":
                    after = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--key":
                    key = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--from":
                    from = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--to":
                    to = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--apply":
                    apply = true;
                    break;
                case "--dry-run":
                    dry = true;
                    break;
                case "--relax":
                    relax = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--profile":
                    profile = ParseEditorialProfile(Need());
                    break;
                case "--lexicon":
                    enableLexicon = true;
                    break;
                case "--no-lexicon":
                    enableLexicon = false;
                    break;
                case "--slop":
                    enableSlop = true;
                    break;
                case "--no-slop":
                    enableSlop = false;
                    break;
                case "--naming":
                    enableNaming = true;
                    break;
                case "--no-naming":
                    enableNaming = false;
                    break;
                default:
                    if (a.StartsWith('-'))
                        throw new InvalidOperationException($"Unknown option: {a}");
                    paths.Add(a);
                    break;
            }
        }

        return new BookCliOptions
        {
            StartDir = start,
            BookFile = bookFile,
            ChaptersDir = chapters,
            Series = series,
            Book = book,
            Title = title,
            Character = character,
            OutPath = outPath,
            After = after,
            Key = key,
            From = from,
            To = to,
            Apply = apply,
            DryRun = dry,
            Relax = relax,
            Json = json,
            EditorialProfile = profile,
            EnableLexicon = enableLexicon,
            EnableSlop = enableSlop,
            EnableNaming = enableNaming,
            Paths = paths,
        };
    }

    static EditorialProfile ParseEditorialProfile(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "fiction" => EditorialProfile.Fiction,
            "nonfiction" => EditorialProfile.Nonfiction,
            "calypso" => EditorialProfile.Calypso,
            _ => throw new InvalidOperationException("editorial --profile must be fiction, nonfiction, or calypso."),
        };
}
