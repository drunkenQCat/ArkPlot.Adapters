using System.IO;
using System.Threading;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class MockGameAdapterTests : IDisposable
{
    private readonly string _dbPath;

    public MockGameAdapterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_test_{Guid.NewGuid():N}.db");
        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
    }

    public void Dispose()
    {
        DbFactory.Reset();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task StoryPipeline_MockGame_ProcessesEndToEnd()
    {
        var pipeline = new StoryPipeline(
            provider: new MockStoryProvider(),
            parser: new MockScriptParser(),
            renderer: new MockTagRenderer(),
            picDescService: new PicDescService());

        var act = new Act { Id = 1, Name = "Mock Event", Lang = "en" };
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "Chapter 1", StoryCode = "ch1" }
        };

        var results = await pipeline.ProcessEventAsync(act, chapters, OutputMode.Readable);

        Assert.Single(results);
        Assert.Equal("Chapter 1", results[0].Title);
        Assert.Contains("Hello from Alice", results[0].Markdown);
        Assert.Contains("Hi from Bob", results[0].Markdown);
    }

    [Fact]
    public async Task StoryPipeline_EmptyChapter_ReturnsEmptyMarkdown()
    {
        var pipeline = new StoryPipeline(
            provider: new EmptyStoryProvider(),
            parser: new MockScriptParser(),
            renderer: new MockTagRenderer(),
            picDescService: new PicDescService());

        var act = new Act { Id = 1, Name = "Empty", Lang = "en" };
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "Empty", StoryCode = "empty" }
        };

        var results = await pipeline.ProcessEventAsync(act, chapters);

        Assert.Single(results);
        Assert.True(string.IsNullOrWhiteSpace(results[0].Markdown));
    }

    [Fact]
    public async Task StoryPipeline_MockGame_PromptMode()
    {
        var pipeline = new StoryPipeline(
            provider: new MockStoryProvider(),
            parser: new MockScriptParser(),
            renderer: new MockTagRenderer(),
            picDescService: new PicDescService());

        var act = new Act { Id = 1, Name = "Mock", Lang = "en" };
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "Ch", StoryCode = "ch" }
        };

        var results = await pipeline.ProcessEventAsync(act, chapters, OutputMode.PromptOptimized);

        Assert.Single(results);
        Assert.NotEmpty(results[0].Markdown);
    }

    [Fact]
    public void IScriptParser_Interface_CanBeImplemented()
    {
        IScriptParser parser = new MockScriptParser();
        Assert.Equal("mock-game", parser.GameId);

        var lines = parser.Parse("line1\nline2", "test");
        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void ITagRenderer_Interface_CanBeImplemented()
    {
        ITagRenderer renderer = new MockTagRenderer();
        Assert.False(renderer.RequiresRulesFile);

        var line = new ScriptLine { OriginalText = "hello" };
        var result = renderer.RenderLine(line);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void IResourceResolver_Interface_CanBeImplemented()
    {
        IResourceResolver resolver = new MockResourceResolver();
        Assert.Null(resolver.NormalizeCharacterCode("unknown"));
        Assert.Equal("portrait:char1", resolver.ResolvePortraitUrl("char1"));
        Assert.Equal("bg:forest", resolver.ResolveBackgroundUrl("forest"));
        Assert.Equal("audio:theme", resolver.ResolveAudioUrl("theme"));
    }
}

// ──────────── Mock implementations ────────────

internal class MockStoryProvider : IStoryDataProvider
{
    public IReadOnlyList<string> SupportedLanguages => ["en"];

    public Task SyncMetadataAsync(string lang) => Task.CompletedTask;

    public Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
        => Task.FromResult<string?>("Alice:Hello from Alice\nBob:Hi from Bob\n");

    public Task<string?> GetLatestVersionAsync(string lang) => Task.FromResult<string?>("v1.0");

    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang) => new();
}

internal class EmptyStoryProvider : IStoryDataProvider
{
    public IReadOnlyList<string> SupportedLanguages => ["en"];
    public Task SyncMetadataAsync(string lang) => Task.CompletedTask;
    public Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
        => Task.FromResult<string?>("");
    public Task<string?> GetLatestVersionAsync(string lang) => Task.FromResult<string?>("v1.0");
    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang) => new();
}

internal class MockScriptParser : IScriptParser
{
    public string GameId => "mock-game";

    public List<ScriptLine> Parse(string rawText, string chapterTitle)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new List<ScriptLine>();

        var lines = new List<ScriptLine>();
        var parts = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].TrimEnd('\r');
            var colonIdx = part.IndexOf(':');
            var entry = new ScriptLine { Index = i, OriginalText = part };

            if (colonIdx > 0)
            {
                entry.CharacterName = part[..colonIdx];
                entry.Dialog = part[(colonIdx + 1)..];
                entry.Type = "dialog";
            }
            else
            {
                entry.Dialog = part;
                entry.Type = "narration";
            }

            lines.Add(entry);
        }

        return lines;
    }

    public HashSet<ResourceRef> CollectResources(List<ScriptLine> lines) => new();
}

internal class MockTagRenderer : ITagRenderer
{
    public bool RequiresRulesFile => false;
    public void LoadRules(string rulesFilePath) { }

    public string RenderLine(ScriptLine line)
    {
        if (!string.IsNullOrEmpty(line.CharacterName))
            return $"**{line.CharacterName}** {line.Dialog}";

        return line.OriginalText;
    }
}

internal class MockResourceResolver : IResourceResolver
{
    public string? NormalizeCharacterCode(string rawName) => null;
    public string ResolvePortraitUrl(string characterCode, string? variant = null) => $"portrait:{characterCode}";
    public string ResolveBackgroundUrl(string bgKey) => $"bg:{bgKey}";
    public string ResolveAudioUrl(string audioKey) => $"audio:{audioKey}";
}
