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
                        // Per-request Authorization avoids mutating shared DefaultRequestHeaders
                        // (not thread-safe) and prevents the header leaking to future endpoints.
                        using var req = new HttpRequestMessage(HttpMethod.Post, $"{config.BackendUrl}/v1/submit") { Content = content };
                        if (config.SessionToken is not null)
                            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.SessionToken);
                        var response = await _http.SendAsync(req, reqCts.Token);

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
                {
                    config = config with { LastSyncAt = DateTime.UtcNow.ToString("O") };
                    _configManager.Save(config);
                }

                foreach (var id in failed)
                    _queue.MarkFailed(id);

                if (authExpired)
                {
                    // Don't wipe the token on first 401 — a momentary 401 (rate-limit,
                    // clock skew) would force the user to re-link unnecessarily. The
                    // unsubmitted batch stays in the queue for the next flush attempt.
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
