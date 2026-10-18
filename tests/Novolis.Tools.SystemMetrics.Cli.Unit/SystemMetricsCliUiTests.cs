using Novolis.IO.SystemMetrics;
using Novolis.Tools.SystemMetrics.Cli;
using Spectre.Console.Testing;

namespace Novolis.Tools.SystemMetrics.Cli.Unit;

public sealed class SystemMetricsCliUiTests
{
    [Test]
    public async Task BuildLive_RendersResourceValuesAndStatuses()
    {
        var sample = new SystemMetricsSample(
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(1.25),
            new CpuMetrics(42.5, 10, 2),
            new MemoryMetrics(4_000, 1_000, 3_000),
            new ProcessMetrics(
                1234,
                "render-job",
                18.75,
                TimeSpan.FromSeconds(2),
                2_000,
                1_500,
                8,
                200,
                100),
            [
                new GpuMetrics(
                    "gpu-0",
                    76.25,
                    new Dictionary<string, double>(),
                    1_500,
                    8_000,
                    null,
                    null,
                    67,
                    null),
            ],
            [
                new NetworkMetrics(
                    "ethernet",
                    10_000,
                    4_000,
                    2_000,
                    1_000,
                    20,
                    10),
            ],
            3,
            null,
            []);
        var console = new TestConsole();

        console.Write(SystemMetricsCliUi.BuildLive(
            SystemMetricsInventory.Empty,
            sample,
            5));

        await Assert.That(console.Output).Contains("42.5%");
        await Assert.That(console.Output).Contains("Process 1234");
        await Assert.That(console.Output).Contains("GPU gpu-0");
        await Assert.That(console.Output).Contains("NET ethernet");
        await Assert.That(console.Output).Contains("5 at ");
    }
}
