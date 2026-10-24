using System.Security.Cryptography;
using System.Text;
using Novolis.Agent.Core;
using Novolis.Agent.Surface;

namespace Novolis.Tools.AgentDev;

public sealed class DevelopmentAgentHost : IAgentHost
{
    public const string DiagnosticToken = "// novolis-agent-error:";

    private readonly string _root;
    private readonly IDevelopmentProcessRunner _runner;
    private readonly AgentSurfaceDefinition _definition;
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentCommandResult> _completed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DevelopmentPendingChange> _pending = new(StringComparer.Ordinal);
    private long _revision = 1;
    private string _phase = "inspect";

    public DevelopmentAgentHost(string root, IDevelopmentProcessRunner runner, AgentSurfaceDefinition? definition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _definition = definition ?? AgentSurfaceDefinition.From<IDevelopmentSurface>();
    }

    public string Root => _root;

    public long StateRevision
    {
        get { lock (_gate) return _revision; }
    }

    public event Action<AgentDecisionEvent>? Decision;

    public event Action<AgentChangedEvent>? Changed;

    public event Action<AgentActionResultEvent>? ActionResult;

    public AgentHello Hello() => _definition.BuildHello(appId: "novolis-agent-dev", appTitle: "Novolis agent development profile");

    public AgentSnapshot Snapshot()
    {
        lock (_gate)
        {
            return Capture();
        }
    }

    public AgentActionsResponse Actions() => new() { Actions = _definition.Actions.ToArray() };

    public AgentCommandResult Continue()
    {
        lock (_gate)
        {
            var result = Accept(new AgentCommand { ActionId = AgentActionIds.Continue }, AgentLifecycle.Applied, "continue", mutate: false);
            Decision?.Invoke(new AgentDecisionEvent { DecisionLine = _phase, Snapshot = result.Snapshot });
            return result;
        }
    }

    public void Subscribe()
    {
    }

    public AgentCommandResult Execute(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            var actionId = command.ActionId?.Trim() ?? "";
            if (actionId.Length == 0)
            {
                return Reject(command, "incomplete", "ActionId required");
            }

            if (!string.IsNullOrWhiteSpace(command.CorrelationId)
                && _completed.TryGetValue(command.CorrelationId, out var prior))
            {
                return prior;
            }

            var action = _definition.Actions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, actionId, StringComparison.Ordinal));
            var rejected = AgentCommandGate.Reject(action, command, _revision);
            if (rejected is not null)
            {
                Publish(rejected);
                return rejected;
            }

            try
            {
                var result = actionId switch
                {
                    DevelopmentActionIds.List => List(command),
                    DevelopmentActionIds.Read => Read(command),
                    DevelopmentActionIds.Diagnostics => Diagnostics(command),
                    DevelopmentActionIds.Build => RunProcess(command, "build"),
                    DevelopmentActionIds.Test => RunProcess(command, "test"),
                    DevelopmentActionIds.Diff => Diff(command),
                    DevelopmentActionIds.Propose => Propose(command),
                    DevelopmentActionIds.Apply => Apply(command),
                    DevelopmentActionIds.Evidence => Evidence(command),
                    _ => Reject(command, "unknown", $"Unknown action '{actionId}'."),
                };
                if (!string.IsNullOrWhiteSpace(result.CorrelationId))
                {
                    _completed[result.CorrelationId] = result;
                }

                Publish(result);
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                var failed = Reject(command, "failed", ex.Message);
                Publish(failed);
                return failed;
            }
        }
    }

    private AgentCommandResult List(AgentCommand command)
    {
        _phase = "inspect";
        var relative = command.Get("path") ?? "";
        var dir = Resolve(relative, createFile: false);
        if (!Directory.Exists(dir))
        {
            return Reject(command, "not-found", $"Directory '{relative}' was not found.");
        }

        var names = Directory.EnumerateFileSystemEntries(dir)
            .Select(path => Path.GetRelativePath(_root, path))
            .Where(path => !IsHidden(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Accept(command, AgentLifecycle.Applied, string.Join('\n', names), mutate: false);
    }

    private AgentCommandResult Read(AgentCommand command)
    {
        _phase = "inspect";
        var relative = command.Get("path") ?? "";
        var file = Resolve(relative, createFile: false);
        if (!File.Exists(file))
        {
            return Reject(command, "not-found", $"File '{relative}' was not found.");
        }

        var text = File.ReadAllText(file);
        return Accept(command, AgentLifecycle.Applied, Redact(text), mutate: false, resource: relative);
    }

    private AgentCommandResult Diagnostics(AgentCommand command)
    {
        _phase = "reproduce";
        var found = Scan();
        var message = found.Count == 0
            ? "no diagnostics"
            : string.Join('\n', found.Select(item => $"{item.Code} {item.ResourceId}: {item.Message}"));
        return Accept(command, AgentLifecycle.Applied, message, mutate: false);
    }

    private AgentCommandResult RunProcess(AgentCommand command, string verb)
    {
        _phase = "reproduce";
        var process = _runner.Run(verb, _root, CancellationToken.None);
        var output = Redact(process.StandardOutput + process.StandardError).Trim();
        if (process.ExitCode != 0)
        {
            return Reject(command, "process-failed", string.IsNullOrWhiteSpace(output) ? $"{verb} exited {process.ExitCode}." : output, mutate: true);
        }

        return Accept(command, AgentLifecycle.Applied, string.IsNullOrWhiteSpace(output) ? $"{verb} completed." : output, mutate: true);
    }

    private AgentCommandResult Diff(AgentCommand command)
    {
        _phase = "hypothesize";
        var pending = FindPending(command);
        if (pending is null)
        {
            return Accept(command, AgentLifecycle.Applied, "No pending change.", mutate: false);
        }

        var current = File.Exists(Resolve(pending.Path, createFile: false))
            ? File.ReadAllText(Resolve(pending.Path, createFile: false))
            : "";
        return Accept(command, AgentLifecycle.Applied, Redact(BuildDiff(pending.Path, current, pending.Content)), mutate: false, resource: pending.Path);
    }

    private AgentCommandResult Propose(AgentCommand command)
    {
        _phase = "change";
        var relative = command.Get("path") ?? "";
        var content = command.Get("content") ?? "";
        Resolve(relative, createFile: false);
        var correlation = string.IsNullOrWhiteSpace(command.CorrelationId) ? Guid.NewGuid().ToString("n") : command.CorrelationId;
        command.CorrelationId = correlation;
        _pending[correlation] = new DevelopmentPendingChange
        {
            CorrelationId = correlation,
            Path = relative.Replace('\\', '/'),
            Content = content,
        };
        return Accept(
            command,
            AgentLifecycle.Preview,
            $"Preview stored for '{relative}'. Apply with approvalToken '{correlation}'.",
            mutate: false,
            resource: relative);
    }

    private AgentCommandResult Apply(AgentCommand command)
    {
        var token = command.ApprovalToken ?? "";
        if (!_pending.TryGetValue(token, out var pending))
        {
            return Reject(command, "approval-required", "Approval token does not match a pending change.");
        }

        var file = Resolve(pending.Path, createFile: true);
        var before = File.Exists(file) ? File.ReadAllText(file) : "";
        File.WriteAllText(file, pending.Content);
        _pending.Remove(token);
        _phase = "verify";
        var result = Accept(command, AgentLifecycle.Applied, $"Applied {pending.Path}.", mutate: true, resource: pending.Path);
        result.Receipt!.Effects =
        [
            new AgentEffect { ResourceId = pending.Path, Kind = "write", Before = Summarize(before), After = Summarize(pending.Content) },
        ];
        return result;
    }

    private AgentCommandResult Evidence(AgentCommand command)
    {
        _phase = "summarize";
        var directory = Path.Combine(_root, ".novolis-agent");
        Directory.CreateDirectory(directory);
        var relative = $".novolis-agent/evidence-{_revision}.json";
        var diagnostics = Scan();
        var body = Redact(System.Text.Json.JsonSerializer.Serialize(new
        {
            revision = _revision,
            phase = _phase,
            diagnostics = diagnostics.Select(item => new { item.Code, item.Message, item.ResourceId }),
        }));
        File.WriteAllText(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)), body);
        var result = Accept(command, AgentLifecycle.Applied, $"Exported {relative}.", mutate: false, resource: relative);
        result.Receipt!.Artifacts =
        [
            new AgentArtifactRef { Id = relative, Kind = "evidence", MediaType = "application/json", Uri = relative, Summary = "Redacted evidence" },
        ];
        return result;
    }

    private AgentCommandResult Accept(AgentCommand command, string lifecycle, string message, bool mutate, string? resource = null)
    {
        if (mutate)
        {
            _revision++;
        }

        var receipt = AgentCommandGate.CreateReceipt(command, lifecycle, true, message, _revision, recovery: "Refresh the snapshot before the next mutation.");
        return new AgentCommandResult
        {
            Ok = true,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            Message = message,
            StateRevision = _revision,
            Receipt = receipt,
            Snapshot = Capture(resource),
        };
    }

    private AgentCommandResult Reject(AgentCommand command, string reason, string message, bool mutate = false)
    {
        if (mutate)
        {
            _revision++;
        }

        var receipt = AgentCommandGate.CreateReceipt(
            command,
            AgentLifecycle.Rejected,
            false,
            message,
            _revision,
            reason,
            "Refresh the snapshot, then retry with a new correlation or the required approval token.");
        var result = new AgentCommandResult
        {
            Ok = false,
            ActionId = command.ActionId,
            CorrelationId = command.CorrelationId,
            ErrorCode = reason,
            Message = message,
            StateRevision = _revision,
            Receipt = receipt,
            Snapshot = Capture(),
        };
        if (!string.IsNullOrWhiteSpace(command.CorrelationId) && reason is not "stale-snapshot" and not "approval-required" and not "invalid-arguments")
        {
            _completed[command.CorrelationId] = result;
        }

        return result;
    }

    private void Publish(AgentCommandResult result)
    {
        var evt = new AgentActionResultEvent
        {
            ActionId = result.ActionId,
            Ok = result.Ok,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            CorrelationId = result.CorrelationId,
            ResourceId = result.Receipt?.Effects.FirstOrDefault()?.ResourceId,
            Severity = result.Ok ? AgentSeverity.Info : AgentSeverity.Warning,
            Receipt = result.Receipt,
            Snapshot = result.Snapshot,
        };
        ActionResult?.Invoke(evt);
        Changed?.Invoke(new AgentChangedEvent
        {
            Reason = result.Ok ? "applied" : "rejected",
            CorrelationId = result.CorrelationId,
            Severity = evt.Severity,
            Snapshot = result.Snapshot,
        });
    }

    private AgentSnapshot Capture(string? resource = null)
    {
        var diagnostics = Scan();
        return new AgentSnapshot
        {
            StateRevision = _revision,
            MetadataVersion = AgentMetadata.Version,
            DocumentName = _root,
            PauseReason = _phase,
            Actions = _definition.Actions.ToArray(),
            Observations =
            [
                new AgentObservation
                {
                    ResourceId = resource ?? _root,
                    StateRevision = _revision,
                    Summary = _phase,
                    Diagnostics = diagnostics.ToArray(),
                    AvailableActionIds = _definition.Actions.Select(action => action.Id).ToArray(),
                },
            ],
        };
    }

    private List<AgentDiagnostic> Scan()
    {
        var found = new List<AgentDiagnostic>();
        if (!Directory.Exists(_root))
        {
            return found;
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_root, file);
            if (IsHidden(relative))
            {
                continue;
            }

            foreach (var line in File.ReadLines(file))
            {
                var index = line.IndexOf(DiagnosticToken, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var body = line[(index + DiagnosticToken.Length)..].Trim();
                var space = body.IndexOf(' ');
                var code = space < 0 ? body : body[..space];
                var message = space < 0 ? body : body[(space + 1)..];
                found.Add(new AgentDiagnostic
                {
                    Severity = AgentSeverity.Danger,
                    Code = code,
                    Message = message,
                    ResourceId = relative.Replace('\\', '/'),
                });
            }
        }

        return found;
    }

    private DevelopmentPendingChange? FindPending(AgentCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ApprovalToken) && _pending.TryGetValue(command.ApprovalToken, out var byToken))
        {
            return byToken;
        }

        var path = command.Get("path");
        return _pending.Values.FirstOrDefault(item =>
            path is null || string.Equals(item.Path, path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
    }

    private string Resolve(string relative, bool createFile)
    {
        var combined = Path.GetFullPath(Path.Combine(_root, relative));
        var root = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(combined, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes the workspace.");
        }

        if (createFile)
        {
            var parent = Path.GetDirectoryName(combined);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
        }

        return combined;
    }

    private static bool IsHidden(string relative) =>
        relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or ".git" or ".novolis-agent");

    private static string BuildDiff(string path, string before, string after)
    {
        var builder = new StringBuilder();
        builder.Append("--- ").AppendLine(path);
        builder.Append("+++ ").AppendLine(path);
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            builder.AppendLine("-" + before.Replace("\r", "").Replace("\n", "\n-"));
            builder.AppendLine("+" + after.Replace("\r", "").Replace("\n", "\n+"));
        }

        return builder.ToString();
    }

    private static string Redact(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value,
            "(?i)(password|secret|token)\\s*[:=]\\s*\\S+",
            "$1=[redacted]");

    private static string Summarize(string value)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return hash[..12];
    }
}
