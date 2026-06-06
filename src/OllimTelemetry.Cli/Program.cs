using ConsoleAppFramework;
using OllimTelemetry.Cli.Commands;
using OllimTelemetry.Cli.Update;
using OllimTelemetry.Core;

if (XdgMigration.TryMigrate())
    Console.Error.WriteLine("[ollim] migrated config to XDG paths (~/.config/ollim, ~/.local/share/ollim)");

var installMethod = Environment.GetEnvironmentVariable("OLLIM_INSTALL_METHOD") ?? "script";
UpdateChecker.ScheduleRefresh();

var app = ConsoleApp.Create();
app.Add("start",       StartCommand.RunAsync);
app.Add("stop",        StopCommand.RunAsync);
app.Add("status",      StatusCommand.RunAsync);
app.Add("config",      ConfigCommand.RunAsync);
app.Add("stats",       StatsCommand.RunAsync);
app.Add("leaderboard", LeaderboardCommand.RunAsync);
app.Add("hook",        HookCommand.RunAsync);
app.Add("unlink",      UnlinkCommand.RunAsync);
app.Add("uninstall",   UninstallCommand.RunAsync);
await app.RunAsync(args);

UpdateChecker.PrintNoticeIfAvailable(installMethod);
