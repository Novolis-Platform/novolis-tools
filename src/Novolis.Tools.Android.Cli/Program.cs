using System.CommandLine;
using System.Text.Json;
using Novolis.IO.Mobile.Android;
using Novolis.Tools.Cli;

var serialOption = new Option<string?>("--serial")
{
    Description = "Target device serial. Required when several ready devices exist.",
};
var jsonOption = new Option<bool>("--json")
{
    Description = "Write one JSON document instead of human-readable output.",
    DefaultValueFactory = _ => false,
};
var timeoutOption = new Option<int>("--timeout")
{
    Description = "Operation timeout in seconds (default: 45).",
    DefaultValueFactory = _ => 45,
};
var yesOption = new Option<bool>("--yes")
{
    Description = "Confirm a destructive or text-injection action.",
    DefaultValueFactory = _ => false,
};
var quietOption = new Option<bool>("--quiet")
{
    Description = "Suppress successful status details where possible.",
    DefaultValueFactory = _ => false,
};
var noColorOption = new Option<bool>("--no-color")
{
    Description = "Disable color in host output.",
    DefaultValueFactory = _ => false,
};

var root = new RootCommand("""
    novolis-android — Android deployment and diagnostics

    The command targets one explicit ready device, or a single ready device
    selected by ANDROID_SERIAL. Use --json for scripts and CI.
    """)
{
    serialOption,
    jsonOption,
    timeoutOption,
    yesOption,
    quietOption,
    noColorOption,
};

var doctor = new Command("doctor", "Check adb, the server, SDK discovery, and device authorization.");
doctor.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var adb = new AndroidDebugBridge();
        var devices = await adb.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        var payload = new
        {
            ok = devices.Any(d => d.State == AdbDeviceState.Device),
            transport = adb.Transport,
            adbPath = adb.AdbPath,
            devices = devices.Select(ToDevicePayload).ToArray(),
        };
        WriteResult(parseResult, payload, () =>
        {
            Console.WriteLine($"transport: {adb.Transport}");
            Console.WriteLine($"adb: {adb.AdbPath}");
            WriteDevicesTable(devices);
        });
        return payload.ok ? ExitCodes.Ok : ExitCodes.Failure;
    }));
root.Subcommands.Add(doctor);

var devicesCommand = new Command("devices", "List connected devices and authorization state.");
var allOption = new Option<bool>("--all")
{
    Description = "Include every device state instead of only ready devices.",
    DefaultValueFactory = _ => false,
};
devicesCommand.Options.Add(allOption);
devicesCommand.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var adb = new AndroidDebugBridge();
        var devices = await adb.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        if (!parseResult.GetValue(allOption))
            devices = devices.Where(d => d.State == AdbDeviceState.Device).ToArray();
        WriteResult(parseResult, devices.Select(ToDevicePayload).ToArray(), () => WriteDevicesTable(devices));
        return ExitCodes.Ok;
    }));
root.Subcommands.Add(devicesCommand);

var info = new Command("info", "Print a technical report for one ready device.");
info.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var adb = new AndroidDebugBridge();
        var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
        if (!target.Ok)
            return Fail(parseResult, target.Failure!);

        var report = await Task.Run(
                () => adb.GetDeviceInfo(target.Device!.Serial),
                cancellationToken)
            .ConfigureAwait(false);
        if (parseResult.GetValue(jsonOption))
        {
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        else
        {
            Console.WriteLine(report.FormatReport());
        }

        return ExitCodes.Ok;
    }));
root.Subcommands.Add(info);

var app = new Command("app", "Inspect, deploy, and control application packages.");
var appInspect = new Command("inspect", "Show package metadata and install state.")
{
    PackageArgument(),
};
appInspect.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var package = parseResult.GetValue((Argument<string>)appInspect.Arguments[0])!;
        AndroidInputValidator.RequirePackageName(package);
        var adb = new AndroidDebugBridge();
        var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
        if (!target.Ok)
            return Fail(parseResult, target.Failure!);

        var infoResult = await adb.TryGetPackageInfoAsync(
                package,
                target.Device!.Serial,
                cancellationToken)
            .ConfigureAwait(false);
        if (infoResult is null)
            return Fail(
                parseResult,
                new AndroidFailure(
                    AndroidFailureKind.DeviceNotFound,
                    $"Package '{package}' is not installed.",
                    target.Device!.Serial));

        WriteResult(parseResult, infoResult, () =>
        {
            Console.WriteLine($"{infoResult.PackageName}");
            Console.WriteLine($"  installed: {infoResult.IsInstalled}");
            Console.WriteLine($"  version: {infoResult.VersionName ?? "—"} ({infoResult.VersionCode?.ToString() ?? "—"})");
            Console.WriteLine($"  apk: {infoResult.ApkPath ?? "—"}");
        });
        return ExitCodes.Ok;
    }));
app.Subcommands.Add(appInspect);

var appInstall = new Command("install", "Validate and install one APK.")
{
    new Argument<string>("apk") { Description = "Path to the APK." },
};
var installPackageOption = new Option<string?>("--package")
{
    Description = "Expected package id for post-install verification.",
};
var launchOption = new Option<bool>("--launch")
{
    Description = "Launch the expected package after installation.",
    DefaultValueFactory = _ => false,
};
var grantOption = new Option<bool>("--grant")
{
    Description = "Grant runtime permissions during replacement.",
    DefaultValueFactory = _ => false,
};
var downgradeOption = new Option<bool>("--allow-downgrade")
{
    Description = "Allow a version-code downgrade.",
    DefaultValueFactory = _ => false,
};
var certificateOption = new Option<string?>("--certificate-sha256")
{
    Description = "Expected installed signing certificate SHA-256 digest.",
};
appInstall.Options.Add(installPackageOption);
appInstall.Options.Add(launchOption);
appInstall.Options.Add(grantOption);
appInstall.Options.Add(downgradeOption);
appInstall.Options.Add(certificateOption);
appInstall.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var apk = parseResult.GetValue((Argument<string>)appInstall.Arguments[0])!;
        var package = parseResult.GetValue(installPackageOption);
        if (package is not null)
            AndroidInputValidator.RequirePackageName(package);
        var adb = new AndroidDebugBridge();
        var installer = new AndroidAppInstaller(adb);
        var result = await installer.InstallAsync(
                apk,
                new ApkInstallOptions
                {
                    Serial = parseResult.GetValue(serialOption),
                    Reinstall = true,
                    GrantPermissions = parseResult.GetValue(grantOption),
                    AllowDowngrade = parseResult.GetValue(downgradeOption),
                    ExpectedPackageName = package,
                    ExpectedSigningCertificateSha256 = parseResult.GetValue(certificateOption),
                    VerifyInstalled = package is not null,
                    LaunchAfterInstall = parseResult.GetValue(launchOption),
                    DeviceWaitTimeout = Timeout(parseResult),
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(
                parseResult,
                new AndroidFailure(
                    result.FailureKind == AndroidFailureKind.Unknown
                        ? AndroidFailureKind.VerificationFailed
                        : result.FailureKind,
                    result.Message,
                    result.Serial));

        WriteResult(parseResult, result, () => Console.WriteLine(result.Message));
        return ExitCodes.Ok;
    }));
app.Subcommands.Add(appInstall);

foreach (var lifecycle in new[]
         {
             ("launch", "Launch a package.", false),
             ("stop", "Force-stop a package.", true),
             ("uninstall", "Uninstall a package.", true),
             ("clear", "Clear package data and cache.", true),
         })
{
    var command = new Command(lifecycle.Item1, lifecycle.Item2)
    {
        PackageArgument(),
    };
    command.SetAction(async (parseResult, cancellationToken) =>
        await RunSafeAsync(parseResult, async () =>
        {
            var package = parseResult.GetValue((Argument<string>)command.Arguments[0])!;
            AndroidInputValidator.RequirePackageName(package);
            if (lifecycle.Item3
                && !Confirm(parseResult, lifecycle.Item1 + " " + package))
            {
                return ExitCodes.Failure;
            }

            var adb = new AndroidDebugBridge();
            var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
            if (!target.Ok)
                return Fail(parseResult, target.Failure!);

            AdbOperationResult result;
            if (lifecycle.Item1 == "launch")
            {
                result = await adb.StartAppAsync(package, target.Device!.Serial, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (lifecycle.Item1 == "stop")
            {
                result = await adb.ForceStopAsync(package, target.Device!.Serial, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (lifecycle.Item1 == "uninstall")
            {
                result = await adb.UninstallAsync(package, target.Device!.Serial, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                var clear = await new AndroidDeviceDiagnostics(adb).ClearDataAsync(
                        package,
                        target.Device!.Serial,
                        cancellationToken)
                    .ConfigureAwait(false);
                result = ToOperationResult(clear, "clear");
            }

            if (!result.Ok)
                return Fail(
                    parseResult,
                    new AndroidFailure(result.FailureKind, result.Message, target.Device!.Serial));
            WriteResult(parseResult, result, () => Console.WriteLine(result.Message));
            return ExitCodes.Ok;
        }));
    app.Subcommands.Add(command);
}

var restart = new Command("restart", "Force-stop and launch a package.")
{
    PackageArgument(),
};
restart.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var package = parseResult.GetValue((Argument<string>)restart.Arguments[0])!;
        AndroidInputValidator.RequirePackageName(package);
        var adb = new AndroidDebugBridge();
        var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
        if (!target.Ok)
            return Fail(parseResult, target.Failure!);

        var stop = await adb.ForceStopAsync(package, target.Device!.Serial, cancellationToken)
            .ConfigureAwait(false);
        var start = stop.Ok
            ? await adb.StartAppAsync(package, target.Device!.Serial, cancellationToken).ConfigureAwait(false)
            : stop;
        if (!start.Ok)
            return Fail(parseResult, new AndroidFailure(start.FailureKind, start.Message, target.Device!.Serial));
        WriteResult(parseResult, start, () => Console.WriteLine($"Restarted {package}."));
        return ExitCodes.Ok;
    }));
app.Subcommands.Add(restart);
root.Subcommands.Add(app);

var logcat = new Command("logcat", "Capture recent device logs.");
var logcatPackageOption = new Option<string?>("--package")
{
    Description = "Retain lines containing this package id.",
};
var logcatLinesOption = new Option<int>("--lines")
{
    Description = "Maximum recent log lines requested from adb.",
    DefaultValueFactory = _ => 500,
};
var clearLogcatOption = new Option<bool>("--clear")
{
    Description = "Clear the device log buffer before capture.",
    DefaultValueFactory = _ => false,
};
var followLogcatOption = new Option<bool>("--follow")
{
    Description = "Follow logcat until cancelled instead of taking a finite snapshot.",
    DefaultValueFactory = _ => false,
};
logcat.Options.Add(logcatPackageOption);
logcat.Options.Add(logcatLinesOption);
logcat.Options.Add(clearLogcatOption);
logcat.Options.Add(followLogcatOption);
logcat.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var package = parseResult.GetValue(logcatPackageOption);
        if (package is not null)
            AndroidInputValidator.RequirePackageName(package);
        var adb = new AndroidDebugBridge();
        var diagnostics = new AndroidDeviceDiagnostics(adb);
        if (parseResult.GetValue(followLogcatOption))
        {
            await foreach (var line in diagnostics.FollowLogcatAsync(
                               new AndroidLogcatOptions
                               {
                                   Serial = parseResult.GetValue(serialOption),
                                   PackageName = package,
                               },
                               cancellationToken)
                           .WithCancellation(cancellationToken))
            {
                Console.WriteLine(line);
            }

            return ExitCodes.Ok;
        }

        var result = await diagnostics.CaptureLogcatAsync(
                new AndroidLogcatOptions
                {
                    Serial = parseResult.GetValue(serialOption),
                    PackageName = package,
                    LastLines = parseResult.GetValue(logcatLinesOption),
                    ClearBeforeCapture = parseResult.GetValue(clearLogcatOption),
                    Timeout = Timeout(parseResult),
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, result.Failure!);
        if (parseResult.GetValue(jsonOption))
            Console.WriteLine(JsonSerializer.Serialize(new { ok = true, text = result.Text }));
        else
            Console.WriteLine(result.Text);
        return ExitCodes.Ok;
    }));
root.Subcommands.Add(logcat);

var screen = new Command("screen", "Capture screen evidence.");
var shot = new Command("shot", "Capture a PNG screenshot.")
{
    new Argument<string>("output") { Description = "PNG output path." },
};
shot.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var output = parseResult.GetValue((Argument<string>)shot.Arguments[0])!;
        var adb = new AndroidDebugBridge();
        var result = await new AndroidDeviceDiagnostics(adb).CaptureScreenshotAsync(
                output,
                parseResult.GetValue(serialOption),
                Timeout(parseResult),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, result.Failure!);
        WriteResult(parseResult, result, () => Console.WriteLine($"Wrote {result.Path}"));
        return ExitCodes.Ok;
    }));
screen.Subcommands.Add(shot);
root.Subcommands.Add(screen);

var ui = new Command("ui", "Capture the Android UIAutomator hierarchy.");
var dump = new Command("dump", "Write UI XML.")
{
    new Argument<string?>("output")
    {
        Description = "Optional XML output path (defaults to ui.xml).",
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => "ui.xml",
    },
};
dump.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var output = parseResult.GetValue((Argument<string?>)dump.Arguments[0]) ?? "ui.xml";
        var adb = new AndroidDebugBridge();
        var result = await new AndroidDeviceDiagnostics(adb).DumpUiAsync(
                output,
                parseResult.GetValue(serialOption),
                Timeout(parseResult),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, result.Failure!);
        WriteResult(parseResult, result, () => Console.WriteLine($"Wrote {result.Path}"));
        return ExitCodes.Ok;
    }));
ui.Subcommands.Add(dump);
root.Subcommands.Add(ui);

var file = new Command("file", "Transfer files through ADB sync.");
var push = new Command("push", "Push a local file.")
{
    new Argument<string>("local"),
    new Argument<string>("remote"),
};
push.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var local = parseResult.GetValue((Argument<string>)push.Arguments[0])!;
        var remote = parseResult.GetValue((Argument<string>)push.Arguments[1])!;
        var adb = new AndroidDebugBridge();
        var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
        if (!target.Ok)
            return Fail(parseResult, target.Failure!);
        var result = await adb.PushAsync(local, remote, target.Device!.Serial, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, new AndroidFailure(result.FailureKind, result.Message, target.Device!.Serial));
        WriteResult(parseResult, result, () => Console.WriteLine(result.Message));
        return ExitCodes.Ok;
    }));
file.Subcommands.Add(push);

var pull = new Command("pull", "Pull a remote file.")
{
    new Argument<string>("remote"),
    new Argument<string>("local"),
};
pull.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var remote = parseResult.GetValue((Argument<string>)pull.Arguments[0])!;
        var local = parseResult.GetValue((Argument<string>)pull.Arguments[1])!;
        var adb = new AndroidDebugBridge();
        var target = await ResolveTargetAsync(adb, parseResult, cancellationToken).ConfigureAwait(false);
        if (!target.Ok)
            return Fail(parseResult, target.Failure!);
        var result = await adb.PullAsync(remote, local, target.Device!.Serial, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, new AndroidFailure(result.FailureKind, result.Message, target.Device!.Serial));
        WriteResult(parseResult, result, () => Console.WriteLine(result.Message));
        return ExitCodes.Ok;
    }));
file.Subcommands.Add(pull);
root.Subcommands.Add(file);

var input = new Command("input", "Perform one controlled input action.");
var tap = new Command("tap", "Tap a screen coordinate.")
{
    new Argument<int>("x"),
    new Argument<int>("y"),
};
tap.SetAction(async (parseResult, cancellationToken) =>
    await RunInputAsync(
        parseResult,
        cancellationToken,
        "tap",
        () => new AndroidDeviceDiagnostics(new AndroidDebugBridge()).TapAsync(
            parseResult.GetValue((Argument<int>)tap.Arguments[0]),
            parseResult.GetValue((Argument<int>)tap.Arguments[1]),
            parseResult.GetValue(serialOption),
            cancellationToken)));
input.Subcommands.Add(tap);

var swipe = new Command("swipe", "Swipe between two screen coordinates.")
{
    new Argument<int>("start-x"),
    new Argument<int>("start-y"),
    new Argument<int>("end-x"),
    new Argument<int>("end-y"),
    new Argument<int?>("duration-ms")
    {
        Arity = ArgumentArity.ZeroOrOne,
        DefaultValueFactory = _ => null,
    },
};
swipe.SetAction(async (parseResult, cancellationToken) =>
    await RunInputAsync(
        parseResult,
        cancellationToken,
        "swipe",
        () => new AndroidDeviceDiagnostics(new AndroidDebugBridge()).SwipeAsync(
            parseResult.GetValue((Argument<int>)swipe.Arguments[0]),
            parseResult.GetValue((Argument<int>)swipe.Arguments[1]),
            parseResult.GetValue((Argument<int>)swipe.Arguments[2]),
            parseResult.GetValue((Argument<int>)swipe.Arguments[3]),
            parseResult.GetValue((Argument<int?>)swipe.Arguments[4]),
            parseResult.GetValue(serialOption),
            cancellationToken)));
input.Subcommands.Add(swipe);

var key = new Command("key", "Send an Android key event.")
{
    new Argument<string>("key"),
};
key.SetAction(async (parseResult, cancellationToken) =>
    await RunInputAsync(
        parseResult,
        cancellationToken,
        "key event",
        () => new AndroidDeviceDiagnostics(new AndroidDebugBridge()).KeyEventAsync(
            parseResult.GetValue((Argument<string>)key.Arguments[0])!,
            parseResult.GetValue(serialOption),
            cancellationToken)));
input.Subcommands.Add(key);

var text = new Command("text", "Inject text into the focused field.")
{
    new Argument<string>("text"),
};
text.SetAction(async (parseResult, cancellationToken) =>
    await RunInputAsync(
        parseResult,
        cancellationToken,
        "text injection",
        () => new AndroidDeviceDiagnostics(new AndroidDebugBridge()).TextAsync(
            parseResult.GetValue((Argument<string>)text.Arguments[0])!,
            parseResult.GetValue(serialOption),
            cancellationToken)));
input.Subcommands.Add(text);
root.Subcommands.Add(input);

var diagnostics = new Command("diagnostics", "Collect redacted evidence.");
var collect = new Command("collect", "Collect device, logs, screenshot, and UI XML.")
{
    new Argument<string>("output-directory"),
};
var collectPackageOption = new Option<string?>("--package")
{
    Description = "Optional package id for package facts and log filtering.",
};
collect.Options.Add(collectPackageOption);
collect.SetAction(async (parseResult, cancellationToken) =>
    await RunSafeAsync(parseResult, async () =>
    {
        var package = parseResult.GetValue(collectPackageOption);
        if (package is not null)
            AndroidInputValidator.RequirePackageName(package);
        var output = parseResult.GetValue((Argument<string>)collect.Arguments[0])!;
        var adb = new AndroidDebugBridge();
        var result = await new AndroidDeviceDiagnostics(adb).CollectBundleAsync(
                output,
                package,
                parseResult.GetValue(serialOption),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.Ok)
            return Fail(parseResult, result.Failure!);
        WriteResult(parseResult, result, () =>
        {
            Console.WriteLine($"Wrote bundle {result.Directory}");
            foreach (var warning in result.Warnings)
                Console.Error.WriteLine($"warning: {warning}");
        });
        return ExitCodes.Ok;
    }));
diagnostics.Subcommands.Add(collect);
root.Subcommands.Add(diagnostics);

return await root.Parse(args).InvokeAsync();

static Argument<string> PackageArgument() =>
    new("package")
    {
        Description = "Android package id.",
    };

TimeSpan Timeout(ParseResult parseResult)
{
    var seconds = parseResult.GetValue(timeoutOption);
    return TimeSpan.FromSeconds(Math.Max(1, seconds));
}

async Task<int> RunSafeAsync(
    ParseResult parseResult,
    Func<Task<int>> action)
{
    try
    {
        return await action().ConfigureAwait(false);
    }
    catch (AndroidOperationException ex)
    {
        return Fail(parseResult, ex.Failure);
    }
    catch (FileNotFoundException ex)
    {
        return Fail(
            parseResult,
            new AndroidFailure(AndroidFailureKind.MissingAdb, ex.Message, Exception: ex));
    }
    catch (OperationCanceledException)
    {
        return Fail(
            parseResult,
            new AndroidFailure(AndroidFailureKind.Cancelled, "Operation cancelled."));
    }
    catch (Exception ex)
    {
        return Fail(
            parseResult,
            new AndroidFailure(AndroidFailureKind.Unknown, ex.Message, Exception: ex));
    }
}

async Task<AndroidTargetResolution> ResolveTargetAsync(
    AndroidDebugBridge adb,
    ParseResult parseResult,
    CancellationToken cancellationToken)
{
    var devices = await adb.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
    return AndroidDeviceSelector.Resolve(
        devices,
        new AndroidTargetOptions
        {
            Serial = parseResult.GetValue(serialOption),
            RequireExplicitWhenMultiple = true,
            RequireReady = true,
        });
}

int Fail(ParseResult parseResult, AndroidFailure failure)
{
    var payload = new
    {
        ok = false,
        error = failure.Kind.ToString(),
        message = failure.Message,
        serial = failure.Serial,
        command = failure.Command,
    };
    if (parseResult.GetValue(jsonOption))
        Console.Error.WriteLine(JsonSerializer.Serialize(payload));
    else
        Console.Error.WriteLine($"error [{failure.Kind}]: {failure.Message}");
    return failure.Kind == AndroidFailureKind.InvalidInput
        ? ExitCodes.Usage
        : ExitCodes.Failure;
}

void WriteResult<T>(
    ParseResult parseResult,
    T payload,
    Action humanWriter)
{
    if (parseResult.GetValue(jsonOption))
        Console.WriteLine(JsonSerializer.Serialize(payload));
    else if (!parseResult.GetValue(quietOption))
        humanWriter();
}

static object ToDevicePayload(AdbDevice device) =>
    new
    {
        serial = device.Serial,
        state = device.State.ToString().ToLowerInvariant(),
        model = device.Model,
        product = device.Product,
        device = device.Device,
        transportId = device.TransportId,
    };

static void WriteDevicesTable(IReadOnlyList<AdbDevice> devices)
{
    Console.WriteLine("SERIAL\tSTATE\tMODEL\tPRODUCT");
    foreach (var device in devices)
    {
        Console.WriteLine(
            $"{device.Serial}\t{device.State.ToString().ToLowerInvariant()}\t" +
            $"{device.Model ?? "—"}\t{device.Product ?? "—"}");
    }
}

bool Confirm(ParseResult parseResult, string summary)
{
    if (parseResult.GetValue(yesOption))
        return true;
    if (Console.IsInputRedirected)
    {
        Console.Error.WriteLine($"error: pass --yes to confirm {summary}.");
        return false;
    }

    Console.Error.Write($"Confirm {summary}? [y/N] ");
    var answer = Console.ReadLine();
    return string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
}

async Task<int> RunInputAsync(
    ParseResult parseResult,
    CancellationToken cancellationToken,
    string summary,
    Func<Task<AdbProcessResult>> action)
{
    if (!Confirm(parseResult, summary))
        return ExitCodes.Failure;
    return await RunSafeAsync(parseResult, async () =>
    {
        var result = await action().ConfigureAwait(false);
        if (!result.Ok)
            return Fail(
                parseResult,
                new AndroidFailure(
                    result.Cancelled
                        ? AndroidFailureKind.Cancelled
                        : AndroidFailureKind.CommandFailed,
                    result.Diagnostic));
        WriteResult(parseResult, result, () => Console.WriteLine("Input action completed."));
        return ExitCodes.Ok;
    }).ConfigureAwait(false);
}

AdbOperationResult ToOperationResult(
    AdbProcessResult result,
    string command) =>
    result.Ok
        ? AdbOperationResult.Success(command, result.StdOut.Trim(), result)
        : AdbOperationResult.Fail(
            command,
            result.Diagnostic,
            result,
            result.Cancelled
                ? AndroidFailureKind.Cancelled
                : AndroidFailureKind.CommandFailed);
