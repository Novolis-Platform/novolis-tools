using Novolis.Agent.Testing;
using Novolis.Tools.AgentDev;

namespace Novolis.Tools.AgentDev.Unit;

public static class DevelopmentScenarios
{
    public static AgentScenario DiagnoseCompilerError() => new()
    {
        Name = "diagnose-compiler-error",
        PermittedCapabilities = ["dev.inspect", "dev.diagnostics", "dev.build", "dev.test", "dev.change", "dev.evidence"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Diagnostics,
                ExpectOk = true,
                ExpectMessageContains = "CS1002",
            },
        ],
    };

    public static AgentScenario RepairAfterApproval() => new()
    {
        Name = "repair-after-approval",
        PermittedCapabilities = ["dev.inspect", "dev.diagnostics", "dev.build", "dev.test", "dev.change", "dev.evidence"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Propose,
                CorrelationId = "proposal-1",
                Params = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = "Program.cs",
                    ["content"] = "class Program { static void Main() { } }\n",
                },
                ExpectOk = true,
                ExpectMessageContains = "proposal-1",
            },
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Apply,
                CorrelationId = "apply-1",
                ApprovalToken = "proposal-1",
                ExpectedRevision = 1,
                ExpectOk = true,
            },
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Diagnostics,
                ExpectOk = true,
                ExpectMessageContains = "no diagnostics",
            },
        ],
    };

    public static AgentScenario AwaitBuild() => new()
    {
        Name = "await-build",
        PermittedCapabilities = ["dev.build"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Build,
                CorrelationId = "build-1",
                AwaitActionResult = true,
                ExpectOk = true,
            },
        ],
    };

    public static AgentScenario RecoverFlakyTest() => new()
    {
        Name = "recover-flaky-test",
        PermittedCapabilities = ["dev.test"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Test,
                CorrelationId = "test-1",
                ExpectOk = false,
                ExpectReason = "process-failed",
                ExpectMessageContains = "flaky",
            },
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Test,
                CorrelationId = "test-2",
                ExpectOk = true,
            },
        ],
    };

    public static AgentScenario RejectUnsafeMutation() => new()
    {
        Name = "reject-unsafe-mutation",
        PermittedCapabilities = ["dev.change"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Apply,
                ExpectOk = false,
                ExpectReason = "approval-required",
            },
        ],
    };

    public static AgentScenario StaleRevision() => new()
    {
        Name = "stale-revision",
        PermittedCapabilities = ["dev.change"],
        Steps =
        [
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Propose,
                CorrelationId = "proposal-stale",
                Params = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = "Program.cs",
                    ["content"] = "class Program { static void Main() { } }\n",
                },
                ExpectOk = true,
            },
            new AgentScenarioStep
            {
                ActionId = DevelopmentActionIds.Apply,
                CorrelationId = "apply-stale",
                ApprovalToken = "proposal-stale",
                ExpectedRevision = 99,
                ExpectOk = false,
                ExpectReason = "stale-snapshot",
            },
        ],
    };
}
