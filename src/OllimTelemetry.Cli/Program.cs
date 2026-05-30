using System.Runtime.InteropServices;
using ConsoleAppFramework;
using OllimTelemetry.Cli.Commands;
using OllimTelemetry.Cli.Daemon;

// Daemon entry point — invoked by the OS service manager, not by users.
// The service template calls: ollim --run-daemon
if (args.Contains("--run-daemon"))
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    // PosixSignalRegistration is required for SIGTERM under NativeAOT on Linux —
    // AppDomain.ProcessExit does not fire reliably in the native runtime.
    // ctx.Cancel = true suppresses the default OS termination so cleanup can run.
    using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
    {
        ctx.Cancel = true;
        cts.Cancel();
    });
    await DaemonRunner.RunAsync(cts.Token);
    return;
}

var app = ConsoleApp.Create();
app.Add("start",     StartCommand.RunAsync);
app.Add("stop",      StopCommand.RunAsync);
app.Add("status",    StatusCommand.RunAsync);
app.Add("config",    ConfigCommand.RunAsync);
app.Add("stats",     StatsCommand.RunAsync);
app.Add("unlink",    UnlinkCommand.RunAsync);
app.Add("uninstall", UninstallCommand.RunAsync);
await app.RunAsync(args);
