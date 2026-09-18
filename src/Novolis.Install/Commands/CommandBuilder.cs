using System.CommandLine;
using Novolis.Install.Paths;
using Novolis.Install.Registry;

namespace Novolis.Install.Commands;

/// <summary>
/// Builds the <c>novolis</c> command-line interface.
/// </summary>
public static class CommandBuilder
{
    /// <summary>
    /// Creates the root command with search, install, list, and diagnostic subcommands.
    /// </summary>
    /// <returns>The configured <see cref="RootCommand"/> instance.</returns>
    public static RootCommand Build()
    {
        var root = new RootCommand("Novolis package installer and launcher");

        var queryArg = new Argument<string>("id") { Description = "Package id" };
        var channelOption = new Option<string>("--channel")
        {
            Description = "Release channel (stable or preview)",
            DefaultValueFactory = _ => "stable",
        };

        var search = new Command("search", "Search the Novolis registry");
        var searchTerm = new Argument<string?>("term") { Arity = ArgumentArity.ZeroOrOne, Description = "Optional search term" };
        search.Add(searchTerm);
        search.SetAction(async (parseResult, cancellationToken) =>
        {
            var term = parseResult.GetValue(searchTerm);
            var index = await RegistryIndex.LoadAsync(new Uri(RegistryOptions.DefaultIndexUrl), cancellationToken);
            var matches = index.Packages
                .Where(p => string.IsNullOrWhiteSpace(term) || p.Id.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Id)
                .ToList();
            if (matches.Count == 0)
            {
                Console.WriteLine("No packages found.");
                return 0;
            }
            foreach (var pkg in matches)
                Console.WriteLine($"{pkg.Id}\t{pkg.Manifest}");
            return 0;
        });

        var info = new Command("info", "Show package metadata from the registry");
        info.Add(queryArg);
        info.SetAction((parseResult, _) =>
        {
            var id = parseResult.GetValue(queryArg)!;
            Console.WriteLine($"Package: {id}");
            Console.WriteLine($"Registry: {RegistryOptions.DefaultIndexUrl}");
            Console.WriteLine("(Manifest resolution not yet implemented.)");
            return Task.FromResult(0);
        });

        var install = new Command("install", "Install a package");
        install.Add(queryArg);
        install.Add(channelOption);
        install.Add(new Option<bool>("--installer") { Description = "Use the platform installer adapter when available" });
        install.SetAction((parseResult, _) =>
        {
            var id = parseResult.GetValue(queryArg)!;
            var channel = parseResult.GetValue(channelOption)!;
            Console.WriteLine($"Install {id} (channel: {channel}) — not yet implemented.");
            Console.WriteLine($"Install root: {NovolisPaths.InstalledPackagesDirectory}");
            return Task.FromResult(1);
        });

        var update = new Command("update", "Update installed packages");
        var updateId = new Argument<string?>("id") { Arity = ArgumentArity.ZeroOrOne };
        update.Add(updateId);
        update.SetAction((parseResult, _) =>
        {
            var id = parseResult.GetValue(updateId);
            Console.WriteLine(id is null ? "Update all — not yet implemented." : $"Update {id} — not yet implemented.");
            return Task.FromResult(1);
        });

        var list = new Command("list", "List installed packages");
        list.SetAction((_, _) =>
        {
            if (!File.Exists(NovolisPaths.StateFile))
            {
                Console.WriteLine("No packages installed.");
                return Task.FromResult(0);
            }
            Console.WriteLine($"State file: {NovolisPaths.StateFile}");
            return Task.FromResult(0);
        });

        var remove = new Command("remove", "Remove an installed package");
        remove.Add(queryArg);
        remove.SetAction((parseResult, _) =>
        {
            Console.WriteLine($"Remove {parseResult.GetValue(queryArg)} — not yet implemented.");
            return Task.FromResult(1);
        });

        var doctor = new Command("doctor", "Check environment and Novolis paths");
        doctor.SetAction((_, _) =>
        {
            Console.WriteLine("Novolis doctor");
            Console.WriteLine($"  .NET runtime: {Environment.Version}");
            Console.WriteLine($"  Data root:    {NovolisPaths.DataRoot}");
            Console.WriteLine($"  Bin shims:    {NovolisPaths.BinShimDirectory}");
            Console.WriteLine($"  Packages:     {NovolisPaths.InstalledPackagesDirectory}");
            Console.WriteLine($"  Registry:     {RegistryOptions.DefaultIndexUrl}");
            return Task.FromResult(0);
        });

        root.Subcommands.Add(search);
        root.Subcommands.Add(info);
        root.Subcommands.Add(install);
        root.Subcommands.Add(update);
        root.Subcommands.Add(list);
        root.Subcommands.Add(remove);
        root.Subcommands.Add(doctor);

        return root;
    }
}
