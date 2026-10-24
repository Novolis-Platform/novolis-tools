using Novolis.Agent.Surface;

namespace Novolis.Tools.AgentDev;

[AgentSurface("agent-dev", HttpPort = 18920, TcpPort = 18921, Description = "Development, debug, and test profile")]
[AgentAction(DevelopmentActionIds.List, Summary = "List files", Params = "path?", Capability = "dev.inspect", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Lists files under the workspace.")]
[AgentAction(DevelopmentActionIds.Read, Summary = "Read a file", Params = "path", Capability = "dev.inspect", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Reads one workspace file.")]
[AgentAction(DevelopmentActionIds.Diagnostics, Summary = "List diagnostics", Capability = "dev.diagnostics", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Scans workspace files for declared diagnostics.")]
[AgentAction(DevelopmentActionIds.Build, Summary = "Run build", Capability = "dev.build", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Runs the build and records its exit code.")]
[AgentAction(DevelopmentActionIds.Test, Summary = "Run tests", Capability = "dev.test", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Runs tests and records the result.")]
[AgentAction(DevelopmentActionIds.Diff, Summary = "Preview a diff", Params = "path?", Capability = "dev.inspect", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Shows the pending change without writing it.")]
[AgentAction(DevelopmentActionIds.Propose, Summary = "Propose a change", Params = "path;content", Capability = "dev.change", SafetyClass = "mutate", Idempotency = "at-most-once", EffectPreview = "Stores a change preview. Nothing is written until change.apply.")]
[AgentAction(DevelopmentActionIds.Apply, Summary = "Apply an approved change", Capability = "dev.change", SafetyClass = "mutate", ApprovalRequired = true, Idempotency = "at-most-once", EffectPreview = "Writes the approved preview into the workspace.")]
[AgentAction(DevelopmentActionIds.Evidence, Summary = "Export evidence", Capability = "dev.evidence", SafetyClass = "read", Idempotency = "repeatable", EffectPreview = "Writes a redacted evidence artifact for the current revision.")]
public interface IDevelopmentSurface
{
}
