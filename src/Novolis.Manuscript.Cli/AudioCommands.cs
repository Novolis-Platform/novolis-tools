using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

namespace Novolis.Manuscript.Cli;

static class AudioCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        var opts = AudioCliOptions.Parse(args);
        if (opts.Help)
        {
            PrintHelp();
            return 0;
        }

        if (!ManuscriptWorkspace.TryOpen(opts.Workspace ?? Directory.GetCurrentDirectory(), out var ws) || ws is null)
            throw new InvalidOperationException("Not a manuscript workspace.");

        var book = ws.Catalog.FindBook(ws.ContentRoot, opts.Series, opts.Book!)
                   ?? throw new FileNotFoundException($"Book not found: {opts.Series}/{opts.Book}");

        var chapters = book.Chapters
            .Where(c => c.Kind == ChapterKind.Chapter)
            .Where(c => SelectChapter(c, opts))
            .Select(c => new AudiobookChapterInput(c.Id, c.Title, c.FilePath))
            .ToList();
        if (chapters.Count == 0)
            throw new InvalidOperationException("No chapters matched the selection.");

        var voice = string.IsNullOrWhiteSpace(opts.VoiceMap)
            ? new VoiceSettings()
            : VoiceMapStore.Load(opts.VoiceMap);

        if (opts.DryRun)
        {
            foreach (var chapter in chapters)
            {
                var markdown = File.ReadAllText(chapter.MarkdownPath);
                var plan = SpeechPlanner.Create(markdown, speakTitle: opts.SpeakTitle);
                Console.WriteLine($"{chapter.Id}: {plan.Segments.Count} segments");
            }

            return 0;
        }

        if (opts.VerifyOnly)
        {
            var outDir = ResolveOutDir(ws.ContentRoot, opts, book);
            AudiobookVerifier.VerifyOrThrow(outDir);
            Console.WriteLine($"Verified audiobook under {outDir}");
            return 0;
        }

        var outputDir = ResolveOutDir(ws.ContentRoot, opts, book);
        Directory.CreateDirectory(outputDir);
        var endpointText = opts.AzureEndpoint ??
            Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT");
        var key = opts.AzureKey ??
            Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Azure Speech synthesis requires an HTTPS endpoint and key. " +
                "Use --azure-endpoint/--azure-key or NOVOLIS_AZURE_SPEECH_ENDPOINT/NOVOLIS_AZURE_SPEECH_KEY.");
        }

        var synthesizer = new AzureSpeechSynthesizer(endpoint, key);
        var pipeline = new AudiobookPipeline(synthesizer);
        var options = new AudiobookOptions
        {
            OutputDirectory = outputDir,
            AssembleMode = opts.Assemble,
            ParallelJobs = opts.Jobs,
            Force = opts.Force,
        };

        var progress = new Progress<AudiobookProgress>(p => Console.WriteLine(p.Message));
        var result = await pipeline.GenerateAsync(book.Id, chapters, voice, options, progress).ConfigureAwait(false);
        Console.WriteLine($"Manifest: {result.ManifestPath}");
        if (result.M4bPath is not null)
            Console.WriteLine($"M4B: {result.M4bPath}");
        if (result.ConcatenatedMp3Path is not null)
            Console.WriteLine($"MP3: {result.ConcatenatedMp3Path}");
        return 0;
    }

    static string ResolveOutDir(string root, AudioCliOptions opts, BookInfo book)
    {
        if (!string.IsNullOrWhiteSpace(opts.OutputDir))
            return Path.GetFullPath(opts.OutputDir);
        var series = book.SeriesId ?? opts.Series ?? "books";
        return string.Equals(series, "books", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, "out", book.Id, "audio")
            : Path.Combine(root, "out", series, book.Id, "audio");
    }

    static bool SelectChapter(ChapterInfo chapter, AudioCliOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ChapterStem))
            return chapter.Id.Equals(opts.ChapterStem, StringComparison.OrdinalIgnoreCase);

        var key = ChapterOrder.GetFilenameSortKey(chapter.FilePath);
        if (opts.From is double from && key < from)
            return false;
        if (opts.To is double to && key > to)
            return false;
        return true;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            novolis-manuscript audio --series ID --book ID [options]

              --voice-map PATH     Voice map YAML (optional)
              --azure-endpoint URI Azure Speech resource endpoint (or environment variable)
              --azure-key KEY      Azure Speech resource key (or environment variable)
              --from N --to N      Chapter order range
              --chapter STEM       Single chapter id/stem
              --jobs N             Parallel synthesis jobs (default 2)
              --force              Regenerate existing chapter audio
              --dry-run            Plan only
              --verify             Verify existing output directory
              --output DIR         Override output directory
              --assemble both|mp3|m4b|none
              --speak-title
              --workspace PATH
            """);
    }
}

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
