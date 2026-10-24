using Novolis.Agent.Core;
using Novolis.Agent.Testing;
using Novolis.Tools.AgentDev;

namespace Novolis.Tools.AgentDev.Unit;

public sealed class DevelopmentAgentHostTests
{
    [Test]
    public async Task Scenarios_cover_diagnosis_approval_build_flake_and_rejection()
    {
        var root = NewRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), "class Program { static void Main() { } // novolis-agent-error: CS1002 missing semicolon\n");
        var runner = new ScriptedProcessRunner();
        runner.Enqueue(new DevelopmentProcessResult());
        runner.Enqueue(new DevelopmentProcessResult { ExitCode = 1, StandardError = "flaky assertion" });
        runner.Enqueue(new DevelopmentProcessResult());
        var host = new DevelopmentAgentHost(root, runner);

        var diagnose = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.DiagnoseCompilerError());
        await Assert.That(diagnose.Passed).IsTrue();

        var repair = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.RepairAfterApproval());
        await Assert.That(repair.Passed).IsTrue();
        await Assert.That(repair.ApprovalCompliant).IsTrue();
        await Assert.That(repair.EvidenceComplete).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(root, "Program.cs"))).DoesNotContain("novolis-agent-error");

        var again = host.Execute(new AgentCommand { ActionId = DevelopmentActionIds.Apply, CorrelationId = "apply-1" });
        await Assert.That(again.StateRevision).IsEqualTo(repair.Trace.Steps[1].StateRevision);

        var build = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.AwaitBuild());
        await Assert.That(build.Passed).IsTrue();

        var flake = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.RecoverFlakyTest());
        await Assert.That(flake.Passed).IsTrue();

        var blocked = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.RejectUnsafeMutation());
        await Assert.That(blocked.Passed).IsTrue();
        var before = host.StateRevision;
        var stale = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.StaleRevision());
        await Assert.That(stale.Passed).IsTrue();
        await Assert.That(host.StateRevision).IsEqualTo(before + 0);
    }

    [Test]
    public async Task Read_redacts_secrets_and_refuses_paths_outside_the_workspace()
    {
        var root = NewRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "secrets.txt"), "password=hunter2\n");
        var host = new DevelopmentAgentHost(root, new ScriptedProcessRunner());
        var read = host.Execute(new AgentCommand { ActionId = DevelopmentActionIds.Read }.With("path", "secrets.txt"));
        await Assert.That(read.Ok).IsTrue();
        await Assert.That(read.Message).Contains("password=[redacted]");
        await Assert.That(read.Message).DoesNotContain("hunter2");

        var escaped = host.Execute(new AgentCommand { ActionId = DevelopmentActionIds.Read }.With("path", "..\\outside.txt"));
        await Assert.That(escaped.Ok).IsFalse();

        var evidence = host.Execute(new AgentCommand { ActionId = DevelopmentActionIds.Evidence });
        await Assert.That(evidence.Ok).IsTrue();
        await Assert.That(evidence.Receipt!.Artifacts.Length).IsEqualTo(1);
        var exported = await File.ReadAllTextAsync(Path.Combine(root, evidence.Receipt.Artifacts[0].Uri!));
        await Assert.That(exported).DoesNotContain("hunter2");
    }

    [Test]
    public async Task Replay_reports_the_first_diverging_action()
    {
        var root = NewRoot();
        var host = new DevelopmentAgentHost(root, new ScriptedProcessRunner());
        var report = await AgentScenarioRunner.RunAsync(host, DevelopmentScenarios.RejectUnsafeMutation());
        var copy = new AgentTrace { Scenario = report.Trace.Scenario, Steps = report.Trace.Steps.ToList() };
        await Assert.That(AgentTraceComparer.FirstDivergence(report.Trace, copy)).IsNull();
        copy.Steps[0] = new AgentTraceStep { ActionId = "change.apply", Ok = true, StateHash = "other" };
        await Assert.That(AgentTraceComparer.FirstDivergence(report.Trace, copy)).IsNotNull();
    }

    private static string NewRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-agent-dev-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }
}
