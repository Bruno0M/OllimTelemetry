using System.Net.Http.Json;
using System.Text.Json;
using OllimTelemetry.Core.Config;
using OllimTelemetry.Core.Queue;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Sync;

public sealed class SyncService
{
    private readonly ConfigManager _configManager;
    private readonly SyncQueue     _queue;
    private readonly HttpClient    _http;
    private readonly string        _clientVersion;

    private CancellationTokenSource? _cts;
    private Task?                     _loopTask;

    public SyncService(ConfigManager configManager, SyncQueue queue, HttpClient http, string clientVersion = "0.1.0")
    {
        _configManager = configManager;
        _queue         = queue;
        _http          = http;
        _clientVersion = clientVersion;
    }

    public void Start()
    {
        _cts      = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoop(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync();
        if (_loopTask is not null)
        {
            try { await _loopTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task RunLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var config = _configManager.LoadOrCreate();
            var delay  = TimeSpan.FromMinutes(config.SyncIntervalMinutes);

            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { break; }

            if (!config.ShareGlobal)
                continue;

            await TrySyncAsync(config, ct);
        }
    }

    private async Task TrySyncAsync(AppConfig config, CancellationToken ct)
    {
        try
        {
            var batches = _queue.Dequeue(50);
            if (batches.Count == 0) return;

            var sent   = new List<long>();
            var failed = new List<long>();

            foreach (var (id, batch) in batches)
            {
                if (ct.IsCancellationRequested) break;

                var payload = new SubmitPayload(
                    config.UserId,
                    batch.Agent,
                    batch.InputTokens,
                    batch.OutputTokens,
                    batch.CacheReadTokens,
                    batch.CacheWriteTokens,
                    batch.PeriodStart,
                    batch.PeriodEnd,
                    _clientVersion
                );

                try
                {
                    var content  = JsonContent.Create(payload, ConfigJsonContext.Default.SubmitPayload);
                    var response = await _http.PostAsync($"{config.BackendUrl}/v1/submit", content, ct);

                    if (response.IsSuccessStatusCode)
                        sent.Add(id);
                    else
                        failed.Add(id);
                }
                catch
                {
                    failed.Add(id);
                }
            }

            if (sent.Count > 0)
            {
                _queue.MarkSent(sent);
                _configManager.Save(config with { LastSyncAt = DateTime.UtcNow.ToString("O") });
            }

            foreach (var id in failed)
                _queue.MarkFailed(id);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ollim] sync error: {ex.Message}");
        }
    }
}
