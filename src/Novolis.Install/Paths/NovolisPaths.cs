namespace Novolis.Install.Paths;

/// <summary>
/// Cross-platform paths for Novolis data, shims, and installed packages.
/// </summary>
public static class NovolisPaths
{
    /// <summary>
    /// Root directory for Novolis application data on the current OS.
    /// </summary>
    public static string DataRoot =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis")
            : OperatingSystem.IsMacOS()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Novolis")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "novolis");

    /// <summary>
    /// Directory where executable shims are placed for the user PATH.
    /// </summary>
    public static string BinShimDirectory =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".novolis", "bin")
            : OperatingSystem.IsLinux()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".novolis", "bin");

    /// <summary>
    /// Directory containing unpacked installed package payloads.
    /// </summary>
    public static string InstalledPackagesDirectory => Path.Combine(DataRoot, "packages");

    /// <summary>
    /// Path to the JSON file tracking installed package versions.
    /// </summary>
    public static string StateFile => Path.Combine(DataRoot, "state.json");
}
