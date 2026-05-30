using OllimTelemetry.Core.Parsing;

namespace OllimTelemetry.Tests;

public sealed class LogParserTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly LogParser _parser = new();

    public LogParserTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempFile(string content)
    {
        var path = Path.Combine(_tempDir, Path.GetRandomFileName() + ".jsonl");
        File.WriteAllText(path, content);
        return path;
    }

    // AC-02: 3 lines (1 with usage, 1 without, 1 with usage) → 2 records with correct counts
    [Fact]
    public void Parse_ReturnsOnlyLinesWithUsage()
    {
        var file = TempFile("""
            {"role":"assistant","usage":{"input_tokens":100,"output_tokens":50,"cache_creation_input_tokens":0,"cache_read_input_tokens":200},"timestamp":"2026-01-01T00:00:00Z"}
            {"role":"user","content":"hello"}
            {"role":"assistant","usage":{"input_tokens":300,"output_tokens":80,"cache_creation_input_tokens":10,"cache_read_input_tokens":0},"timestamp":"2026-01-01T00:01:00Z"}
            """);

        var records = _parser.Parse(file, 0, out _, "claude-code");

        Assert.Equal(2, records.Count);
        Assert.Equal(100, records[0].InputTokens);
        Assert.Equal(50,  records[0].OutputTokens);
        Assert.Equal(200, records[0].CacheReadTokens);
        Assert.Equal(0,   records[0].CacheWriteTokens);
        Assert.Equal(300, records[1].InputTokens);
        Assert.Equal(10,  records[1].CacheWriteTokens);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsNoRecords()
    {
        var file = TempFile(string.Empty);

        var records = _parser.Parse(file, 0, out var newOffset, "claude-code");

        Assert.Empty(records);
        Assert.Equal(0, newOffset);
    }

    [Fact]
    public void Parse_DeltaRead_OnlyReturnsNewLines()
    {
        var file = TempFile("""
            {"role":"assistant","usage":{"input_tokens":100,"output_tokens":10},"timestamp":"2026-01-01T00:00:00Z"}
            """);

        // first parse — consume the existing line
        _parser.Parse(file, 0, out var offset1, "claude-code");
        Assert.NotEqual(0, offset1);

        // append a new line
        File.AppendAllText(file, """
            {"role":"assistant","usage":{"input_tokens":999,"output_tokens":1},"timestamp":"2026-01-01T00:02:00Z"}
            """);

        // parse from previous offset — should return only the new line
        var records = _parser.Parse(file, offset1, out _, "claude-code");

        Assert.Single(records);
        Assert.Equal(999, records[0].InputTokens);
    }

    [Fact]
    public void Parse_MalformedLines_AreSkipped()
    {
        var file = TempFile("""
            not valid json at all
            {"role":"assistant","usage":{"input_tokens":42,"output_tokens":7},"timestamp":"2026-01-01T00:00:00Z"}
            """);

        var records = _parser.Parse(file, 0, out _, "claude-code");

        Assert.Single(records);
        Assert.Equal(42, records[0].InputTokens);
    }

    [Fact]
    public void Parse_SetsAgentFromParameter()
    {
        var file = TempFile("""
            {"role":"assistant","usage":{"input_tokens":1,"output_tokens":1},"timestamp":"2026-01-01T00:00:00Z"}
            """);

        var records = _parser.Parse(file, 0, out _, "my-agent");

        Assert.Equal("my-agent", records[0].Agent);
    }
}
