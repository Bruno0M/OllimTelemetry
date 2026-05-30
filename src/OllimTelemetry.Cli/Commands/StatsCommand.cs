using OllimTelemetry.Core.Queue;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OllimTelemetry.Cli.Commands;

public sealed class StatsCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        using var queue   = new SyncQueue();
        var cutoff        = DateTime.UtcNow.AddDays(-7).ToString("O");
        var batches       = queue.GetBatchesSince(cutoff);

        var table = new Table()
            .AddColumn("Date")
            .AddColumn(new TableColumn("Input").RightAligned())
            .AddColumn(new TableColumn("Output").RightAligned())
            .AddColumn(new TableColumn("Cache Read").RightAligned())
            .AddColumn(new TableColumn("Cache Write").RightAligned())
            .AddColumn(new TableColumn("Total").RightAligned());

        var grouped = batches
            .GroupBy(b => DateTime.Parse(b.PeriodStart).Date)
            .OrderByDescending(g => g.Key);

        foreach (var day in grouped)
        {
            var input      = day.Sum(b => b.InputTokens);
            var output     = day.Sum(b => b.OutputTokens);
            var cacheRead  = day.Sum(b => b.CacheReadTokens);
            var cacheWrite = day.Sum(b => b.CacheWriteTokens);
            var total      = input + output + cacheRead + cacheWrite;

            table.AddRow(
                day.Key.ToString("yyyy-MM-dd"),
                input.ToString("N0"),
                output.ToString("N0"),
                cacheRead.ToString("N0"),
                cacheWrite.ToString("N0"),
                $"[bold]{total:N0}[/]"
            );
        }

        if (!table.Rows.Any())
            AnsiConsole.MarkupLine("[dim]No token data for the last 7 days.[/]");
        else
            AnsiConsole.Write(table);

        return Task.FromResult(0);
    }
}
