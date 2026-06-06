using Microsoft.Data.Sqlite;
using OllimTelemetry.Models;

namespace OllimTelemetry.Core.Queue;

public sealed class SyncQueue : IDisposable
{
    private readonly SqliteConnection _conn;

    public SyncQueue(string? dbPath = null)
    {
        var path = dbPath ?? OllimPaths.DbFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA cache_size   = -200;

            CREATE TABLE IF NOT EXISTS file_offsets (
                file_path  TEXT PRIMARY KEY,
                byte_offset INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS pending_batches (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                agent               TEXT    NOT NULL,
                input_tokens        INTEGER NOT NULL DEFAULT 0,
                output_tokens       INTEGER NOT NULL DEFAULT 0,
                cache_read_tokens   INTEGER NOT NULL DEFAULT 0,
                cache_write_tokens  INTEGER NOT NULL DEFAULT 0,
                period_start        TEXT    NOT NULL,
                period_end          TEXT    NOT NULL,
                retry_count         INTEGER NOT NULL DEFAULT 0,
                next_retry_at       TEXT    NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();
        try
        {
            using var alter = _conn.CreateCommand();
            alter.CommandText = "ALTER TABLE pending_batches ADD COLUMN repo_name TEXT";
            alter.ExecuteNonQuery();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // column already exists — idempotent
        }
    }

    public bool HasAnyOffsets()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM file_offsets";
        return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
    }

    public long GetOffset(string filePath)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT byte_offset FROM file_offsets WHERE file_path = $p";
        cmd.Parameters.AddWithValue("$p", filePath);
        var result = cmd.ExecuteScalar();
        return result is long l ? l : 0L;
    }

    public void SetOffset(string filePath, long offset)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO file_offsets (file_path, byte_offset)
            VALUES ($p, $o)
            ON CONFLICT(file_path) DO UPDATE SET byte_offset = excluded.byte_offset;
            """;
        cmd.Parameters.AddWithValue("$p", filePath);
        cmd.Parameters.AddWithValue("$o", offset);
        cmd.ExecuteNonQuery();
    }

    public void SetOffsetAndEnqueue(string filePath, long offset, SyncBatch batch)
    {
        using var tx  = _conn.BeginTransaction();
        using var off = _conn.CreateCommand();
        off.Transaction = tx;
        off.CommandText = """
            INSERT INTO file_offsets (file_path, byte_offset)
            VALUES ($p, $o)
            ON CONFLICT(file_path) DO UPDATE SET byte_offset = excluded.byte_offset;
            """;
        off.Parameters.AddWithValue("$p", filePath);
        off.Parameters.AddWithValue("$o", offset);
        off.ExecuteNonQuery();

        using var enq = _conn.CreateCommand();
        enq.Transaction = tx;
        enq.CommandText = """
            INSERT INTO pending_batches
                (agent, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, period_start, period_end, repo_name)
            VALUES ($agent, $i, $o, $cr, $cw, $ps, $pe, $rn);
            """;
        enq.Parameters.AddWithValue("$agent", batch.Agent);
        enq.Parameters.AddWithValue("$i",     batch.InputTokens);
        enq.Parameters.AddWithValue("$o",     batch.OutputTokens);
        enq.Parameters.AddWithValue("$cr",    batch.CacheReadTokens);
        enq.Parameters.AddWithValue("$cw",    batch.CacheWriteTokens);
        enq.Parameters.AddWithValue("$ps",    batch.PeriodStart);
        enq.Parameters.AddWithValue("$pe",    batch.PeriodEnd);
        enq.Parameters.AddWithValue("$rn",    (object?)batch.RepoName ?? DBNull.Value);
        enq.ExecuteNonQuery();

        tx.Commit();
    }

    public void Enqueue(SyncBatch batch)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pending_batches
                (agent, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, period_start, period_end, repo_name)
            VALUES ($agent, $i, $o, $cr, $cw, $ps, $pe, $rn);
            """;
        cmd.Parameters.AddWithValue("$agent", batch.Agent);
        cmd.Parameters.AddWithValue("$i",     batch.InputTokens);
        cmd.Parameters.AddWithValue("$o",     batch.OutputTokens);
        cmd.Parameters.AddWithValue("$cr",    batch.CacheReadTokens);
        cmd.Parameters.AddWithValue("$cw",    batch.CacheWriteTokens);
        cmd.Parameters.AddWithValue("$ps",    batch.PeriodStart);
        cmd.Parameters.AddWithValue("$pe",    batch.PeriodEnd);
        cmd.Parameters.AddWithValue("$rn",    (object?)batch.RepoName ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<(long Id, SyncBatch Batch)> Dequeue(int maxItems = 50)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, agent, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, period_start, period_end, repo_name
            FROM pending_batches
            WHERE next_retry_at <= datetime('now')
            ORDER BY id ASC
            LIMIT $max;
            """;
        cmd.Parameters.AddWithValue("$max", maxItems);

        var results = new List<(long, SyncBatch)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id    = reader.GetInt64(0);
            var batch = new SyncBatch(
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)
            );
            results.Add((id, batch));
        }
        return results;
    }

    public IReadOnlyList<SyncBatch> GetBatchesSince(string isoTimestamp)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT agent, input_tokens, output_tokens, cache_read_tokens, cache_write_tokens, period_start, period_end, repo_name
            FROM pending_batches
            WHERE period_start >= $since
            ORDER BY period_start DESC;
            """;
        cmd.Parameters.AddWithValue("$since", isoTimestamp);

        var results = new List<SyncBatch>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SyncBatch(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)
            ));
        }
        return results;
    }

    public void MarkSent(IEnumerable<long> ids)
    {
        using var tx  = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM pending_batches WHERE id = $id";
        var param = cmd.Parameters.Add("$id", SqliteType.Integer);
        foreach (var id in ids)
        {
            param.Value = id;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void MarkFailed(long id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE pending_batches
            SET retry_count   = retry_count + 1,
                next_retry_at = datetime('now', '+' || MIN(CAST(POWER(2, retry_count + 1) AS INTEGER), 60) || ' minutes')
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
