using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Arknights;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class ArknightsStoryProviderTests
{
    private readonly ArknightsStoryProvider _provider = new();

    [Fact]
    public void SupportedLanguages_ContainsZhCN()
    {
        Assert.Contains("zh_CN", _provider.SupportedLanguages);
    }

    [Fact]
    public void SupportedLanguages_ContainsMultipleLangs()
    {
        Assert.True(_provider.SupportedLanguages.Count >= 4);
    }

    [Fact]
    public void GetChapterUrls_EmptyChapters_ReturnsEmpty()
    {
        var result = _provider.GetChapterUrls(new List<StoryChapter>(), "zh_CN");
        Assert.Empty(result);
    }

    [Fact]
    public void GetChapterUrls_SingleChapter_ReturnsSingleEntry()
    {
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "测试章节", StoryCode = "level_act_1" }
        };
        var result = _provider.GetChapterUrls(chapters, "zh_CN");

        Assert.Single(result);
        Assert.True(result.ContainsKey("测试章节"));
        Assert.Contains("level_act_1", result["测试章节"].Url);
    }

    [Fact]
    public void GetChapterUrls_UrlContainsStoryCode()
    {
        var chapters = new List<StoryChapter>
        {
            new() { Id = 1, StoryName = "章节", StoryCode = "story_123" }
        };
        var result = _provider.GetChapterUrls(chapters, "zh_CN");

        Assert.Contains("story_123", result["章节"].Url);
    }

    [Fact]
    public void GetChapterUrls_ChapterId_IsPreserved()
    {
        var chapters = new List<StoryChapter>
        {
            new() { Id = 42, StoryName = "章节", StoryCode = "test" }
        };
        var result = _provider.GetChapterUrls(chapters, "zh_CN");
        Assert.Equal(42, result["章节"].ChapterId);
    }
}
