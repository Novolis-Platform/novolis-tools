using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class DocsSiteBuilderTests
{
    [Test]
    public async Task Build_Creates_Repo_Site_With_Sidebar_And_Docs_Landing()
    {
        var corpus = Path.Combine(Path.GetTempPath(), "novolis-docs-corpus-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "novolis-docs-site-" + Guid.NewGuid().ToString("N"));
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-sample", "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "README.md"),
                """
                # Sample docs

                Welcome to the sample library docs.
                """);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "getting-started.md"),
                """
                # Getting started

                Install the package and call `Hello()`.

                See [design](design.md).
                """);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "design.md"),
                """
                # Design

                Layer edges point downward.
                """);

            var count = DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
            });

            await Assert.That(count).IsEqualTo(3);
            await Assert.That(File.Exists(Path.Combine(output, "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "novolis-sample", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "novolis-sample", "getting-started.html"))).IsTrue();

            var catalog = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
            await Assert.That(catalog).Contains(">Docs<");
            await Assert.That(catalog).Contains(">Source<");
            await Assert.That(catalog).Contains("novolis-sample/index.html");

            var landing = await File.ReadAllTextAsync(Path.Combine(output, "novolis-sample", "index.html"));
            await Assert.That(landing).Contains("docs-nav");
            await Assert.That(landing).Contains("Required");
            await Assert.That(landing).Contains("getting-started.html");
            await Assert.That(landing).Contains("Welcome to the sample library docs");

            var gettingStarted = await File.ReadAllTextAsync(Path.Combine(output, "novolis-sample", "getting-started.html"));
            await Assert.That(gettingStarted).Contains("Install the package");
            await Assert.That(gettingStarted).Contains("design.html");
            await Assert.That(gettingStarted).Contains("class=\"active\"");
        }
        finally
        {
            if (Directory.Exists(corpus))
            {
                Directory.Delete(corpus, recursive: true);
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public async Task Build_Generates_Overview_When_Readme_Missing()
    {
        var corpus = Path.Combine(Path.GetTempPath(), "novolis-docs-corpus-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "novolis-docs-site-" + Guid.NewGuid().ToString("N"));
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-bare", "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "getting-started.md"), "# Getting started\n\nHi.\n");

            DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
            });

            var landing = await File.ReadAllTextAsync(Path.Combine(output, "novolis-bare", "index.html"));
            await Assert.That(landing).Contains("generated overview");
            await Assert.That(landing).Contains("getting-started.html");
        }
        finally
        {
            if (Directory.Exists(corpus))
            {
                Directory.Delete(corpus, recursive: true);
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public async Task Build_Publishes_Profile_Tokens_And_OpenGraph_Banner()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-docs-og-" + Guid.NewGuid().ToString("N"));
        var corpus = Path.Combine(root, "corpus");
        var output = Path.Combine(root, "site");
        var assets = Path.Combine(root, "assets");
        var brand = Path.Combine(root, "brand");
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-sample", "docs");
            Directory.CreateDirectory(docsDir);
            Directory.CreateDirectory(assets);
            Directory.CreateDirectory(Path.Combine(brand, "banners"));
            Directory.CreateDirectory(Path.Combine(brand, "generated"));
            await File.WriteAllTextAsync(Path.Combine(docsDir, "README.md"), "# Sample\n\nHello.\n");
            await File.WriteAllTextAsync(Path.Combine(assets, "profile.css"), ":root { --ngp-background: #080D1C; }\n");
            await File.WriteAllTextAsync(Path.Combine(assets, "site.css"), "body { background: var(--ngp-background); }\n");
            await File.WriteAllTextAsync(Path.Combine(brand, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            await File.WriteAllTextAsync(
                Path.Combine(brand, "banners", "novolis-sample.svg"),
                "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            await File.WriteAllTextAsync(
                Path.Combine(brand, "generated", "logo-social.svg"),
                "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

            DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
                AssetsDirectory = assets,
                BrandDirectory = brand,
                BaseUrl = "https://novolis-platform.github.io/.github/",
            });

            await Assert.That(File.Exists(Path.Combine(output, "assets", "profile.css"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "assets", "brand", "logo-social.svg"))).IsTrue();

            var catalog = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
            await Assert.That(catalog).Contains("assets/profile.css");
            await Assert.That(catalog).Contains("og:image");
            await Assert.That(catalog).Contains("assets/brand/logo-social.svg");
            await Assert.That(catalog).Contains("rel=\"canonical\"");
            await Assert.That(catalog).Contains("theme-color");

            var landing = await File.ReadAllTextAsync(Path.Combine(output, "novolis-sample", "index.html"));
            await Assert.That(landing).Contains("og:image");
            await Assert.That(landing).Contains("assets/banners/novolis-sample.svg");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public async Task Catalog_Links_Each_Repos_Latest_Release_And_App_Downloads()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-docs-downloads-" + Guid.NewGuid().ToString("N"));
        var corpus = Path.Combine(root, "corpus");
        var output = Path.Combine(root, "site");
        var statusPath = Path.Combine(root, "status.json");
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-sample", "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "README.md"), "# Sample\n\nHello.\n");
            var releaseUrl = "https://github.com/Novolis-Platform/novolis-sample/releases/tag/v9";
            var snapshot = new OrgStatusSnapshot
            {
                GeneratedAt = "2026-10-02 05:00 UTC",
                Org = "Novolis-Platform",
                Repos =
                [
                    new OrgStatusRepo
                    {
                        Name = "novolis-sample",
                        PackageCount = 1,
                        GprVersion = "2026.1.1.1",
                        ReleaseTag = "v9",
                        ReleaseUrl = releaseUrl,
                    },
                ],
                Releases =
                [
                    new OrgStatusRelease
                    {
                        Repo = "novolis-apps",
                        Tag = "v2026.1.0.45",
                        Published = "2026-09-30 18:00 UTC",
                        Url = "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v2026.1.0.45",
                        Channel = "1 release asset",
                        Assets =
                        [
                            new OrgStatusAsset
                            {
                                Name = "ReachSetup-2026.1.0.45-win-x64.exe",
                                Size = 74_521_335,
                                Url = "https://github.com/Novolis-Platform/novolis-apps/releases/download/v2026.1.0.45/ReachSetup-2026.1.0.45-win-x64.exe",
                            },
                        ],
                    },
                ],
            };
            await File.WriteAllTextAsync(statusPath, System.Text.Json.JsonSerializer.Serialize(snapshot, OrgStatusJson.Options));

            DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
                StatusPath = statusPath,
                BaseUrl = "https://novolis-platform.github.io/.github/",
            });

            var catalog = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
            await Assert.That(catalog).Contains($"""href="{releaseUrl}">Latest release</a>""");
            await Assert.That(catalog).Contains("id=\"downloads\"");
            await Assert.That(catalog).Contains("ReachSetup-2026.1.0.45-win-x64.exe");
            await Assert.That(catalog).Contains("packages?repo_name=novolis-sample");
            await Assert.That(catalog).Contains("href=\"#downloads\">Downloads</a>");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
