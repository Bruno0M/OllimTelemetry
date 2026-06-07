using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
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

    public SyncService(ConfigManager configManager, SyncQueue queue, HttpClient http, string clientVersion = "0.3.0")
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

        _http.DefaultRequestHeaders.Authorization = config.SessionToken is not null
            ? new AuthenticationHeaderValue("Bearer", config.SessionToken)
            : null;

        try
        {
            bool authExpired = false;
            while (!ct.IsCancellationRequested)
            {
                var batches = _queue.Dequeue(50);
                if (batches.Count == 0) break;

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
                        using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        reqCts.CancelAfter(TimeSpan.FromSeconds(5));
                        var content  = JsonContent.Create(payload, ConfigJsonContext.Default.SubmitPayload);
                        var response = await _http.PostAsync($"{config.BackendUrl}/v1/submit", content, reqCts.Token);

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            authExpired = true;
                            break;
                        }

                        if (response.IsSuccessStatusCode)
                            sent.Add(id);
                        else
                            failed.Add(id);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Per-request timeout — treat as a transient failure and let retry handle it.
                        failed.Add(id);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        failed.Add(id);
                    }
                }

                _queue.MarkSent(sent);

                if (sent.Count > 0)
                    _configManager.Save(config with { LastSyncAt = DateTime.UtcNow.ToString("O") });

                foreach (var id in failed)
                    _queue.MarkFailed(id);

                if (authExpired)
                {
                    _configManager.Save(config with { SessionToken = null, GitHubLogin = null });
                    await Console.Error.WriteLineAsync(
                        "[ollim] session expired — run `ollim link` to re-authenticate");
                    break;
                }

                // If every batch in this page failed, the backend is down — stop retrying.
                if (sent.Count == 0) break;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ollim] sync error: {ex.Message}");
        }
    }
}
