using System.IO;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow;
using Xunit;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class ArknightsIntegrationTests : IDisposable
{
    private readonly string _dbPath;

    public ArknightsIntegrationTests()
    {
        var prefixDb = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(prefixDb))
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_integ_{Guid.NewGuid():N}.db");
            File.Copy(prefixDb, _dbPath, overwrite: true);
            DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
        }
        else
        {
            _dbPath = "";
        }
    }

    public void Dispose()
    {
        DbFactory.Reset();
        if (!string.IsNullOrEmpty(_dbPath))
            try { File.Delete(_dbPath); } catch { }
    }

    private bool HasPrefixDb => !string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath);

    [Fact]
    public void PrefixDb_ScriptParser_ParsesRealChapter()
    {
        if (!HasPrefixDb) return;

        var db = DbFactory.GetClient();
        var chapter = db.Queryable<StoryChapter>().First();
        if (chapter == null) return;

        var plot = db.Queryable<Plot>().First(p => p.Title == chapter.StoryName);
        if (plot == null) return;

        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToList();

        if (entries.Count == 0) return;

        var rawText = string.Join("\n", entries.Select(e => e.OriginalText));
        var parser = new ArknightsScriptParser();
        var result = parser.Parse(rawText, chapter.StoryName);

        Assert.NotEmpty(result);
        Assert.True(result.Count > 0);
    }

    [Fact]
    public void PrefixDb_ScriptParser_ExtractsDialogFromRealData()
    {
        if (!HasPrefixDb) return;

        var db = DbFactory.GetClient();
        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => !string.IsNullOrEmpty(e.Dialog))
            .Take(10)
            .ToList();

        if (entries.Count == 0) return;

        var rawText = string.Join("\n", entries.Select(e => e.OriginalText));
        var parser = new ArknightsScriptParser();
        var result = parser.Parse(rawText, "test");

        var dialogs = result.Where(l => !string.IsNullOrEmpty(l.Dialog)).ToList();
        Assert.NotEmpty(dialogs);
    }

    [Fact]
    public void PrefixDb_ParsedEntries_HaveTypes()
    {
        if (!HasPrefixDb) return;

        var db = DbFactory.GetClient();
        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.Type != null && e.Type != "")
            .Take(50)
            .ToList();

        Assert.NotEmpty(entries);

        var types = entries.Select(e => e.Type).Distinct().ToList();
        Assert.True(types.Count > 1, "Should have multiple distinct types");
    }

    [Fact]
    public void PrefixDb_ParsedEntries_HaveDialogs()
    {
        if (!HasPrefixDb) return;

        var db = DbFactory.GetClient();
        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => !string.IsNullOrEmpty(e.Dialog))
            .Take(20)
            .ToList();

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Dialog)));
    }

    [Fact]
    public async Task PrefixDb_StoryPipeline_ReadableMode()
    {
        if (!HasPrefixDb) return;

        var db = DbFactory.GetClient();
        var act = db.Queryable<Act>().First();
        if (act == null) return;

        var chapters = db.Queryable<StoryChapter>().Take(1).ToList();
        if (chapters.Count == 0) return;

        var pipeline = new StoryPipeline(
            provider: new OfflineStoryProvider(db),
            parser: new ArknightsScriptParser(),
            renderer: new ArknightsTagRenderer(),
            picDescService: new PicDescService());

        var results = await pipeline.ProcessEventAsync(act, chapters);

        Assert.NotEmpty(results);
    }
}

/// <summary>
/// 离线数据提供者：从本地 DB 读取已缓存的剧情数据，不联网。
/// </summary>
internal class OfflineStoryProvider : Core.Interfaces.IStoryDataProvider
{
    private readonly SqlSugar.SqlSugarClient _db;

    public OfflineStoryProvider(SqlSugar.SqlSugarClient db) { _db = db; }

    public IReadOnlyList<string> SupportedLanguages => ["zh_CN"];

    public Task SyncMetadataAsync(string lang) => Task.CompletedTask;

    public Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
    {
        var plot = _db.Queryable<Plot>().First(p => p.Title == chapter.StoryName);
        if (plot == null) return Task.FromResult<string?>(null);

        var entries = _db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToList();

        var rawText = string.Join("\n", entries.Select(e => e.OriginalText));
        return Task.FromResult<string?>(rawText);
    }

    public Task<string?> GetLatestVersionAsync(string lang) => Task.FromResult<string?>("offline");

    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang) => new();
}
