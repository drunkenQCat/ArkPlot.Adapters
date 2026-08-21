using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class PicDescEnricherTests
{
    /// <summary>
    /// 立绘行若含多个资源 URL（立绘 + 背景等），PicFacts 只保留主立绘（第一个 URL）的 YAML 事实，
    /// 不再把其余 URL 的事实用换行拼接——否则同一 <aside> 会塞进多段独立 YAML，字段（colors 等）重复。
    /// </summary>
    [Fact]
    public async Task Enrich_PortraitEntry_MultipleUrls_KeepsOnlyMainFacts()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult($"描述 {url}"),
            extractFacts: desc => Task.FromResult(
                desc.Contains("portrait_main")
                    ? "hair: [银色, 长发, 刘海]\nclothing: [外套, 短裤]\ncolors: [银, 蓝]"
                    : "lighting: [昏暗, 微弱]\nspace: [室内, 窄小]"),
            skipCache: true);

        var entry = new ScriptLine
        {
            Type = "character",
            CharacterCode = "char_auto",
            ResourceUrls = new List<string>
            {
                "https://media.prts.wiki/portrait_main.png",
                "https://media.prts.wiki/bg_room.png",
            },
        };

        await PicDescEnricher.EnrichAsync(new List<ScriptLine> { entry }, svc);

        // 只保留主立绘（角色模板）事实；背景的 YAML 段不得混入同一个 aside
        Assert.Contains("hair:", entry.PicFacts);
        Assert.DoesNotContain("lighting:", entry.PicFacts);

        // 散文描述仍保留全部 URL 信息，不因 facts 收缩而丢描述
        Assert.Contains("portrait_main", entry.PicDesc);
        Assert.Contains("bg_room", entry.PicDesc);
    }

    /// <summary>
    /// 背景行含多张背景图时，scene-facts 同样只保留主背景（第一个 URL）的事实，
    /// 避免空值角色模板等第二段 YAML 混入场景描述。
    /// </summary>
    [Fact]
    public async Task Enrich_BackgroundEntry_MultipleUrls_NoSecondYamlBlock()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult($"描述 {url}"),
            extractFacts: desc => Task.FromResult(
                desc.Contains("bg_main")
                    ? "lighting: [晴, 明亮]\nspace: [海边, 开阔]"
                    : "hair: []\nclothing: []\ncolors: []"),
            skipCache: true);

        var entry = new ScriptLine
        {
            Type = "background",
            ResourceUrls = new List<string>
            {
                "https://media.prts.wiki/bg_main.png",
                "https://media.prts.wiki/bg_overlay.png",
            },
        };

        await PicDescEnricher.EnrichAsync(new List<ScriptLine> { entry }, svc);

        Assert.Contains("lighting:", entry.PicFacts);
        Assert.DoesNotContain("hair:", entry.PicFacts);
    }

    /// <summary>
    /// 单一 URL 且全部字段为空时，行为与旧逻辑一致（facts 为空字符串）。
    /// </summary>
    [Fact]
    public async Task Enrich_SingleUrl_NoFacts_LeavesPicFactsEmpty()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult("描述"),
            extractFacts: _ => Task.FromResult(""),
            skipCache: true);

        var entry = new ScriptLine
        {
            Type = "character",
            ResourceUrls = new List<string> { "https://media.prts.wiki/solo.png" },
        };

        await PicDescEnricher.EnrichAsync(new List<ScriptLine> { entry }, svc);

        Assert.Equal("", entry.PicFacts);
        Assert.Contains("描述", entry.PicDesc);
    }
}