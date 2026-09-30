namespace Novolis.Tools.Docs.Cli;

/// <summary>Default workspace paths for novolis-docs maintainer verbs.</summary>
public static class DocsCliPaths
{
    /// <summary>Resolves workspace root from an explicit path or cwd heuristics.</summary>
    public static string ResolveWorkspaceRoot(string? root)
    {
        if (!string.IsNullOrWhiteSpace(root))
        {
            return Path.GetFullPath(root);
        }

        var cwd = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cwd, "novolis-governance")))
        {
            return cwd;
        }

        var parent = Directory.GetParent(cwd)?.FullName;
        if (!string.IsNullOrEmpty(parent) && Directory.Exists(Path.Combine(parent, "novolis-governance")))
        {
            return parent;
        }

        return cwd;
    }

    /// <summary>Resolves the .github brand checkout path.</summary>
    public static string ResolveBrandRoot(string workspaceRoot, string? brandRoot) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(brandRoot)
            ? Path.Combine(workspaceRoot, ".github")
            : brandRoot);

    /// <summary>Resolves novolis-governance under the workspace.</summary>
    public static string ResolveGovernanceRoot(string workspaceRoot) =>
        Path.Combine(workspaceRoot, "novolis-governance");

    /// <summary>Resolves governance graphical-profile JSON.</summary>
    public static string ResolveProfilePath(string workspaceRoot) =>
        Path.Combine(ResolveGovernanceRoot(workspaceRoot), "build", "graphical-profile", "profile.json");

    /// <summary>Resolves sync-repo-package-index-readme.ps1.</summary>
    public static string ResolvePackageIndexScript(string workspaceRoot) =>
        Path.Combine(ResolveGovernanceRoot(workspaceRoot), "scripts", "sync-repo-package-index-readme.ps1");
}
