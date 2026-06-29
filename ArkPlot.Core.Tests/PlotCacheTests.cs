using System.IO;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using SqlSugar;
using Xunit;

// PlotStatus.Parsed → PlotStatus.Parsed, PlotStatus.Downloaded → PlotStatus.Downloaded

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class PlotCacheTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqlSugarClient _db;

    public PlotCacheTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_test_{Guid.NewGuid():N}.db");
        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
        _db = DbFactory.GetClient();
    }

    public void Dispose()
    {
        DbFactory.Reset();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task SaveAsync_And_TryLoadAsync_Roundtrip()
    {
        var plot = new Plot { ActId = 1, Title = "测试章节", StoryChapterId = 10 };
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, OriginalText = "第一行", MdText = "**第一行**" },
            new() { Index = 1, OriginalText = "第二行", MdText = "第二行" },
        };

        await PlotCache<ScriptLine>.SaveAsync(plot, entries, PlotStatus.Parsed, _db);
        var result = await PlotCache<ScriptLine>.TryLoadAsync(1, "测试章节", _db);

        Assert.NotNull(result);
        Assert.Equal("测试章节", result.Value.Plot.Title);
        Assert.Equal(2, result.Value.Entries.Count);
        Assert.Equal("第一行", result.Value.Entries[0].OriginalText);
    }

    [Fact]
    public async Task GetCachedTitlesAsync_ReturnsOnlyStatus2()
    {
        var plot1 = new Plot { ActId = 1, Title = "已完成", StoryChapterId = 1 };
        var plot2 = new Plot { ActId = 1, Title = "未完成", StoryChapterId = 2 };
        await PlotCache<ScriptLine>.SaveAsync(plot1, [new() { Index = 0, OriginalText = "x" }], PlotStatus.Parsed, _db);
        await PlotCache<ScriptLine>.SaveAsync(plot2, [new() { Index = 0, OriginalText = "y" }], PlotStatus.Downloaded, _db);

        var titles = await PlotCache<ScriptLine>.GetCachedTitlesAsync(1, _db);

        Assert.Contains("已完成", titles);
        Assert.DoesNotContain("未完成", titles);
    }

    [Fact]
    public async Task TryLoadAsync_NotCached_ReturnsNull()
    {
        var result = await PlotCache<ScriptLine>.TryLoadAsync(999, "不存在", _db);
        Assert.Null(result);
    }

    [Fact]
    public async Task CleanupEmptyPlotsAsync_RemovesEmptyEntries()
    {
        var plot = new Plot { ActId = 1, Title = "空章节", StoryChapterId = 1 };
        var emptyEntries = new List<ScriptLine>
        {
            new() { Index = 0, OriginalText = "" },
            new() { Index = 1, OriginalText = "  " },
        };
        await PlotCache<ScriptLine>.SaveAsync(plot, emptyEntries, PlotStatus.Parsed, _db);

        var cleaned = await PlotCache<ScriptLine>.CleanupEmptyPlotsAsync(1, _db);

        Assert.Equal(1, cleaned);
        var result = await PlotCache<ScriptLine>.TryLoadAsync(1, "空章节", _db);
        Assert.Null(result);
    }

    [Fact]
    public async Task CleanupEmptyPlotsAsync_KeepsNonEmptyEntries()
    {
        var plot = new Plot { ActId = 1, Title = "有内容", StoryChapterId = 1 };
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, OriginalText = "有效内容" },
        };
        await PlotCache<ScriptLine>.SaveAsync(plot, entries, PlotStatus.Parsed, _db);

        var cleaned = await PlotCache<ScriptLine>.CleanupEmptyPlotsAsync(1, _db);

        Assert.Equal(0, cleaned);
        var result = await PlotCache<ScriptLine>.TryLoadAsync(1, "有内容", _db);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SaveAsync_Upsert_UpdatesExistingPlot()
    {
        var plot = new Plot { ActId = 1, Title = "更新测试", StoryChapterId = 100 };
        var entries1 = new List<ScriptLine>
        {
            new() { Index = 0, OriginalText = "旧内容" },
        };
        await PlotCache<ScriptLine>.SaveAsync(plot, entries1, PlotStatus.Parsed, _db);

        var entries2 = new List<ScriptLine>
        {
            new() { Index = 0, OriginalText = "新内容" },
            new() { Index = 1, OriginalText = "新行" },
        };
        await PlotCache<ScriptLine>.SaveAsync(plot, entries2, PlotStatus.Parsed, _db);

        var result = await PlotCache<ScriptLine>.TryLoadAsync(1, "更新测试", _db);
        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Entries.Count);
        Assert.Equal("新内容", result.Value.Entries[0].OriginalText);
    }

    [Fact]
    public async Task SaveAsync_Status1_NotReturnedByGetCachedTitles()
    {
        var plot = new Plot { ActId = 1, Title = "仅下载", StoryChapterId = 1 };
        await PlotCache<ScriptLine>.SaveAsync(plot, [new() { Index = 0, OriginalText = "x" }], PlotStatus.Downloaded, _db);

        var titles = await PlotCache<ScriptLine>.GetCachedTitlesAsync(1, _db);
        Assert.Empty(titles);
    }

    [Fact]
    public async Task TryLoadAsync_EntriesAreOrderedByIndex()
    {
        var plot = new Plot { ActId = 1, Title = "排序测试", StoryChapterId = 1 };
        var entries = new List<ScriptLine>
        {
            new() { Index = 2, OriginalText = "第三" },
            new() { Index = 0, OriginalText = "第一" },
            new() { Index = 1, OriginalText = "第二" },
        };
        await PlotCache<ScriptLine>.SaveAsync(plot, entries, PlotStatus.Parsed, _db);

        var result = await PlotCache<ScriptLine>.TryLoadAsync(1, "排序测试", _db);
        Assert.NotNull(result);
        Assert.Equal("第一", result.Value.Entries[0].OriginalText);
        Assert.Equal("第二", result.Value.Entries[1].OriginalText);
        Assert.Equal("第三", result.Value.Entries[2].OriginalText);
    }
}
