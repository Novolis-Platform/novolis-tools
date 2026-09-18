using Novolis.Xsd.Generator;

namespace Novolis.Xsd.Tool;

internal static class Program
{
    private static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "ubl";
        var scope = GetFlag(args, "--scope");
        var repo = GetFlag(args, "--repo");

        if (!string.IsNullOrWhiteSpace(repo))
            repoRoot = Path.GetFullPath(repo);

        return command switch
        {
            "ubl" => GenerateUbl(repoRoot, scope),
            "ubl-base" or "ubl-lean" => GenerateUblBase(repoRoot),
            "peppol" => GeneratePeppol(repoRoot),
            "help" or "--help" or "-h" => PrintHelp(),
            _ => Fail($"Unknown command '{command}'. Use: ubl | ubl-base | peppol")
        };
    }

    private static int GenerateUbl(string repoRoot, string? scope)
    {
        var schemaRoot = Path.Combine(repoRoot, "schemas", "ubl-2.1");
        var output = Path.Combine(repoRoot, "src", "Novolis.Xsd.Ubl", "Generated");
        Console.WriteLine($"Generating UBL Wire (roslyn) from {schemaRoot}");
        Console.WriteLine($"Output: {output}");

        IReadOnlySet<string>? scopeNames = scope?.Equals("invoice", StringComparison.OrdinalIgnoreCase) == true
            ? new HashSet<string>(StringComparer.Ordinal) { "Invoice" }
            : null;

        new XsdCodeGenerator().GenerateFromDirectory(schemaRoot, new XsdGenerationOptions
        {
            RootNamespace = "Novolis.Xsd.Ubl",
            OutputDirectory = output,
            ScopeLocalNames = scopeNames,
            DocumentRootInterfaceName = "IUblDocument",
            Profile = "Wire",
            ExcludeSignatureNamespaces = true,
            Log = Console.WriteLine
        });

        Console.WriteLine("Done.");
        return 0;
    }

    private static int GenerateUblBase(string repoRoot)
    {
        var schemaRoot = Path.Combine(repoRoot, "schemas", "ubl-2.1");
        var output = Path.Combine(repoRoot, "src", "Novolis.Xsd.Ubl.Lean", "Generated");
        Console.WriteLine($"Generating UBL Base (StripEmbedded) from {schemaRoot}");
        Console.WriteLine($"Output: {output}");

        new XsdCodeGenerator().GenerateBase(schemaRoot, new XsdGenerationOptions
        {
            RootNamespace = "Novolis.Xsd.Ubl.Lean",
            OutputDirectory = output,
            ScopeLocalNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Invoice", "CreditNote", "Reminder"
            },
            Profile = "Base",
            SpineInterfaceName = "IBillingDocumentBase",
            SpineDocumentRootNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Invoice", "CreditNote", "Reminder"
            },
            ExcludeSignatureNamespaces = true,
            Log = Console.WriteLine
        });

        Console.WriteLine("Done.");
        return 0;
    }

    private static int GeneratePeppol(string repoRoot)
    {
        var schemaRoot = Path.Combine(repoRoot, "schemas", "peppol", "sbdh");
        var output = Path.Combine(repoRoot, "src", "Novolis.Xsd.Peppol", "Generated");
        Console.WriteLine($"Generating Peppol SBDH Wire (roslyn) from {schemaRoot}");
        Console.WriteLine($"Output: {output}");

        new XsdCodeGenerator().GenerateFromDirectory(schemaRoot, new XsdGenerationOptions
        {
            RootNamespace = "Novolis.Xsd.Peppol",
            OutputDirectory = output,
            DocumentRootInterfaceName = null,
            Profile = "Wire",
            ExcludeSignatureNamespaces = true,
            Log = Console.WriteLine
        });

        Console.WriteLine("Done.");
        return 0;
    }

    private static string? GetFlag(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("Novolis.Xsd.Tool");
        Console.WriteLine("  ubl [--scope invoice] [--repo PATH]   Regenerate Novolis.Xsd.Ubl/Generated via Roslyn Wire");
        Console.WriteLine("  ubl-base [--repo PATH]                Regenerate Novolis.Xsd.Ubl.Lean/Generated (StripEmbedded Bases)");
        Console.WriteLine("  ubl-lean [--repo PATH]                Alias for ubl-base");
        Console.WriteLine("  peppol [--repo PATH]                  Regenerate Novolis.Xsd.Peppol/Generated from SBDH 1.3 XSDs");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Novolis.Xsd.slnx"))
                || Directory.Exists(Path.Combine(dir.FullName, "schemas", "ubl-2.1")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
