using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.FakeGame.Tests;

[Collection("DbTests")]
public class FakeGameScriptParserTests
{
    private readonly FakeGameScriptParser _parser = new();

    [Fact]
    public void GameId_IsFakeGame()
    {
        Assert.Equal("fake-game", _parser.GameId);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var result = _parser.Parse("", "test");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleDialog_MapsFields()
    {
        var json = @"[{""type"":""dialog"",""speaker"":""Alice"",""speaker_code"":""char_alice"",""text"":""你好""}]";
        var result = _parser.Parse(json, "test");

        Assert.Single(result);
        Assert.Equal("dialog", result[0].Type);
        Assert.Equal("Alice", result[0].CharacterName);
        Assert.Equal("char_alice", result[0].CharacterCode);
        Assert.Equal("你好", result[0].Dialog);
    }

    [Fact]
    public void Parse_MultipleEntries_PreservesOrder()
    {
        var json = @"[{""type"":""bg_change"",""value"":""forest""},{""type"":""dialog"",""speaker"":""Alice"",""text"":""你好""},{""type"":""narration"",""text"":""夜幕降临了。""}]";
        var result = _parser.Parse(json, "test");

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Index);
        Assert.Equal(1, result[1].Index);
        Assert.Equal(2, result[2].Index);
    }

    [Fact]
    public void Parse_TypeMapping_MapsRawToGeneric()
    {
        var json = @"[{""type"":""bg_change"",""value"":""forest""},{""type"":""play_music"",""value"":""theme""},{""type"":""dialog"",""speaker"":""Bob"",""text"":""嗨""}]";
        var result = _parser.Parse(json, "test");

        Assert.Equal("background", result[0].Type);
        Assert.Equal("audio", result[1].Type);
        Assert.Equal("dialog", result[2].Type);
    }

    [Fact]
    public void Parse_NoSpeaker_NarrationLine()
    {
        var json = @"[{""type"":""narration"",""text"":""夜幕降临了。""}]";
        var result = _parser.Parse(json, "test");

        Assert.Single(result);
        Assert.Equal("narration", result[0].Type);
        Assert.Equal("", result[0].CharacterName);
        Assert.Equal("夜幕降临了。", result[0].Dialog);
    }

    [Fact]
    public void CollectResources_ExtractsUrls()
    {
        var json = @"[{""type"":""bg_change"",""value"":""forest""}]";
        var lines = _parser.Parse(json, "test");
        var resources = _parser.CollectResources(lines);

        Assert.Single(resources);
        var res = resources.First();
        Assert.Equal("background", res.ResourceType);
    }

    [Fact]
    public void CollectResources_DialogLine_NoResources()
    {
        var json = @"[{""type"":""dialog"",""speaker"":""Alice"",""text"":""你好""}]";
        var lines = _parser.Parse(json, "test");
        var resources = _parser.CollectResources(lines);

        Assert.Empty(resources);
    }
}

[Collection("DbTests")]
public class FakeGameTagRendererTests
{
    [Fact]
    public void RequiresRulesFile_IsFalse()
    {
        var renderer = new FakeGameTagRenderer();
        Assert.False(renderer.RequiresRulesFile);
    }

    [Fact]
    public void RenderLine_Dialog_UsesTemplate()
    {
        var renderer = new FakeGameTagRenderer();
        var line = new ScriptLine
        {
            Type = "dialog",
            CharacterName = "Alice",
            Dialog = "你好",
            OriginalText = "hello"
        };

        var result = renderer.RenderLine(line);
        Assert.Equal("**Alice** 你好", result);
    }

    [Fact]
    public void RenderLine_Narration_ReturnsDialog()
    {
        var renderer = new FakeGameTagRenderer();
        var line = new ScriptLine
        {
            Type = "narration",
            Dialog = "夜幕降临了。",
            OriginalText = "night falls"
        };

        var result = renderer.RenderLine(line);
        Assert.Equal("夜幕降临了。", result);
    }

    [Fact]
    public void RenderLine_NoDialog_ReturnsOriginal()
    {
        var renderer = new FakeGameTagRenderer();
        var line = new ScriptLine
        {
            Type = "bg_change",
            OriginalText = "background changed"
        };

        var result = renderer.RenderLine(line);
        Assert.Equal("background changed", result);
    }
}

[Collection("DbTests")]
public class FakeGameResourceResolverTests
{
    private readonly FakeGameResourceResolver _resolver = new();

    [Fact]
    public void ResolvePortraitUrl_ReturnsExpectedUrl()
    {
        var url = _resolver.ResolvePortraitUrl("char_alice");
        Assert.Contains("char_alice", url);
        Assert.Contains("portrait", url);
    }

    [Fact]
    public void ResolveBackgroundUrl_ReturnsExpectedUrl()
    {
        var url = _resolver.ResolveBackgroundUrl("forest");
        Assert.Contains("forest", url);
        Assert.Contains("bg", url);
    }

    [Fact]
    public void ResolveAudioUrl_ReturnsExpectedUrl()
    {
        var url = _resolver.ResolveAudioUrl("theme");
        Assert.Contains("theme", url);
        Assert.Contains("bgm", url);
    }
}

[Collection("DbTests")]
public class FakeGamePipelineTests
{
    [Fact]
    public async Task StoryPipeline_EndToEnd_ReadableMode()
    {
        var json = @"[{""type"":""bg_change"",""value"":""forest""},{""type"":""dialog"",""speaker"":""Alice"",""speaker_code"":""char_alice"",""text"":""欢迎来到森林""},{""type"":""dialog"",""speaker"":""Bob"",""speaker_code"":""char_bob"",""text"":""这里好美""},{""type"":""narration"",""text"":""他们继续前行。""}]";

        var pipeline = new StoryPipeline(
            provider: new OfflineFakeGameProvider(json),
            parser: new FakeGameScriptParser(),
            renderer: new FakeGameTagRenderer(),
            picDescService: new PicDescService());

        var act = new Act { Id = 1, Name = "Fake Event", Lang = "en" };
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "Chapter 1", StoryCode = "ch1" }
        };

        var results = await pipeline.ProcessEventAsync(act, chapters, OutputMode.Readable);

        Assert.Single(results);
        var markdown = results[0].Markdown;
        Assert.Contains("Alice", markdown);
        Assert.Contains("欢迎来到森林", markdown);
        Assert.Contains("Bob", markdown);
    }

    [Fact]
    public async Task StoryPipeline_EndToEnd_PromptMode()
    {
        var json = @"[{""type"":""dialog"",""speaker"":""Alice"",""text"":""你好""},{""type"":""narration"",""text"":""结束""}]";

        var pipeline = new StoryPipeline(
            provider: new OfflineFakeGameProvider(json),
            parser: new FakeGameScriptParser(),
            renderer: new FakeGameTagRenderer(),
            picDescService: new PicDescService());

        var act = new Act { Id = 1, Name = "Fake", Lang = "en" };
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "Ch", StoryCode = "ch" }
        };

        var results = await pipeline.ProcessEventAsync(act, chapters, OutputMode.PromptOptimized);

        Assert.Single(results);
        Assert.NotEmpty(results[0].Markdown);
    }
}

internal class OfflineFakeGameProvider : IStoryDataProvider
{
    private readonly string _json;
    public OfflineFakeGameProvider(string json) { _json = json; }
    public IReadOnlyList<string> SupportedLanguages => ["en"];
    public Task SyncMetadataAsync(string lang) => Task.CompletedTask;
    public Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
        => Task.FromResult<string?>(_json);
    public Task<string?> GetLatestVersionAsync(string lang) => Task.FromResult<string?>("v1.0");
    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang) => new();
}
