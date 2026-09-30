using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CoverageWorkspaceTests
{
    [Test]
    public async Task ExpandNames_Splits_Commas_And_Trims()
    {
        var names = CoverageWorkspace.ExpandNames(["a, b", "c"]);
        await Assert.That(names).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task RepoAssemblyFilter_Uses_Home_Prefixes()
    {
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-gaming")).IsEqualTo("+Novolis.Game*");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-xsd")).IsEqualTo("+Novolis.Xsd*");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-analyzers"))
            .Contains("Novolis.Analyzers");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-analyzers"))
            .Contains("-Novolis.Analyzers.Licensing");
    }

    [Test]
    public async Task ReadExcludes_Merges_File_And_Extra()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-ex-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, """
                # comment
                novolis-apps
                novolis-raylib
                """);
            var set = CoverageWorkspace.ReadExcludes(path, ["novolis-audio"]);
            await Assert.That(set.Contains("novolis-apps")).IsTrue();
            await Assert.That(set.Contains("novolis-raylib")).IsTrue();
            await Assert.That(set.Contains("novolis-audio")).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
