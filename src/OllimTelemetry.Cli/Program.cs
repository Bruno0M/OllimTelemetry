using OllimTelemetry.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("ollim");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<StartCommand>("start")
          .WithDescription("Start the background daemon");

    config.AddCommand<StopCommand>("stop")
          .WithDescription("Stop the daemon");

    config.AddCommand<StatusCommand>("status")
          .WithDescription("Show daemon status and local token stats");

    config.AddCommand<ConfigCommand>("config")
          .WithDescription("Open config file in $EDITOR (or print path)");

    config.AddCommand<StatsCommand>("stats")
          .WithDescription("Print token usage for last 7 days");

    config.AddCommand<UnlinkCommand>("unlink")
          .WithDescription("Opt out and stop syncing (keeps local data)");

    config.AddCommand<UninstallCommand>("uninstall")
          .WithDescription("Remove daemon, config, and all local data");

    config.AddCommand<DaemonCommand>("daemon")
          .WithDescription("Run the telemetry loop (invoked by the OS service manager)")
          .IsHidden();
});

return app.Run(args);
