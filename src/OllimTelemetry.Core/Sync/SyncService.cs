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

    public SyncService(ConfigManager configManager, SyncQueue queue, HttpClient http, string clientVersion = "0.1.0")
    {
        _configManager = configManager;
        _queue         = queue;
        _http          = http;
        _clientVersion = clientVersion;
    }

    /// <summary>
    /// Performs a single sync pass: drains the queue and POSTs to the backend.
    /// Skips silently if ShareGlobal is disabled. HTTP failures are caught and
    /// logged — the batch stays in SQLite for the next call.
    /// </summary>
    public async Task FlushOnceAsync(CancellationToken ct = default)
    {
        var config = _configManager.LoadOrCreate();
        if (!config.ShareGlobal) return;

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
                    _clientVersion,
                    config.ShareRepoName ? batch.RepoName : null
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
                _configManager.Save(config with { LastSyncAt = DateTime.UtcNow.ToString("O") });

            _queue.MarkSent(sent);

            foreach (var id in failed)
                _queue.MarkFailed(id);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ollim] sync error: {ex.Message}");
        }
    }
}
