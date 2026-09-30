using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

namespace Novolis.Manuscript.Cli;

sealed class AudioCliOptions
{
    public bool Help { get; init; }
    public string? Workspace { get; init; }
    public string? Series { get; init; }
    public string? Book { get; init; }
    public string? VoiceMap { get; init; }
    public string? AzureEndpoint { get; init; }
    public string? AzureKey { get; init; }
    public string? ChapterStem { get; init; }
    public string? OutputDir { get; init; }
    public double? From { get; init; }
    public double? To { get; init; }
    public int Jobs { get; init; } = 2;
    public bool Force { get; init; }
    public bool DryRun { get; init; }
    public bool VerifyOnly { get; init; }
    public bool SpeakTitle { get; init; } = true;
    public AudiobookAssembleMode Assemble { get; init; } = AudiobookAssembleMode.Both;

    public static AudioCliOptions Parse(string[] args)
    {
        string? workspace = null, series = null, book = null, voice = null, chapter = null, output = null;
        string? azureEndpoint = null, azureKey = null;
        double? from = null, to = null;
        var jobs = 2;
        var force = false;
        var dry = false;
        var verify = false;
        var speak = true;
        var help = false;
        var assemble = AudiobookAssembleMode.Both;
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Need() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException($"Missing value for {a}");
            switch (a)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--workspace":
                    workspace = Need();
                    break;
                case "--series":
                    series = Need();
                    break;
                case "--book":
                    book = Need();
                    break;
                case "--voice-map":
                    voice = Need();
                    break;
                case "--azure-endpoint":
                    azureEndpoint = Need();
                    break;
                case "--azure-key":
                    azureKey = Need();
                    break;
                case "--chapter":
                    chapter = Need();
                    break;
                case "--output":
                    output = Need();
                    break;
                case "--from":
                    from = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--to":
                    to = double.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--jobs":
                    jobs = int.Parse(Need(), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dry = true;
                    break;
                case "--verify":
                    verify = true;
                    break;
                case "--speak-title":
                    speak = true;
                    break;
                case "--no-speak-title":
                    speak = false;
                    break;
                case "--assemble":
                    assemble = Enum.Parse<AudiobookAssembleMode>(Need(), ignoreCase: true);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option: {a}");
            }
        }

        if (!help && string.IsNullOrWhiteSpace(book))
            throw new InvalidOperationException("--book is required.");

        return new AudioCliOptions
        {
            Help = help,
            Workspace = workspace,
            Series = series,
            Book = book,
            VoiceMap = voice,
            AzureEndpoint = azureEndpoint,
            AzureKey = azureKey,
            ChapterStem = chapter,
            OutputDir = output,
            From = from,
            To = to,
            Jobs = jobs,
            Force = force,
            DryRun = dry,
            VerifyOnly = verify,
            SpeakTitle = speak,
            Assemble = assemble,
        };
    }
}
