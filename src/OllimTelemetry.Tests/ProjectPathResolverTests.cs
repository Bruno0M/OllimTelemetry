using OllimTelemetry.Core.Parsing;

namespace OllimTelemetry.Tests;

public class ProjectPathResolverTests
{
    [Fact]
    public void Resolve_SimpleProject_ReturnsProjectName()
    {
        var path = "/home/user/.claude/projects/-home-user-dev-MyProject/session.jsonl";
        Assert.Equal("MyProject", ProjectPathResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_CapitalisedProject_ReturnsProjectName()
    {
        var path = "/home/user/.claude/projects/-home-user-dev-OllimTelemetry/abc.jsonl";
        Assert.Equal("OllimTelemetry", ProjectPathResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_DashedProjectName_ReturnsLastSegment()
    {
        // Known limitation: dashed names return only the last dash-separated token
        var path = "/home/user/.claude/projects/-home-user-dev-my-project/session.jsonl";
        Assert.Equal("project", ProjectPathResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_EmptyPath_ReturnsNull()
    {
        Assert.Null(ProjectPathResolver.Resolve(""));
    }

    [Fact]
    public void Resolve_FileWithNoParentDirectory_ReturnsNull()
    {
        Assert.Null(ProjectPathResolver.Resolve("justfile.jsonl"));
    }
}
