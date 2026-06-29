using System.IO;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Services;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class StorySyncServiceTests : IDisposable
{
    private readonly string _dbPath;

    public StorySyncServiceTests()
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
    public void GetRepoByLang_ZhCN()
    {
        Assert.Equal("Kengxxiao/ArknightsGameData", StorySyncService.GetRepoByLang("zh_CN"));
    }

    [Fact]
    public void GetRepoByLang_OtherLang()
    {
        Assert.Equal("ArknightsAssets/ArknightsGamedata", StorySyncService.GetRepoByLang("en_US"));
    }

    [Fact]
    public void GetTableUrl_ContainsLang()
    {
        var url = StorySyncService.GetTableUrl("zh_CN");
        Assert.Contains("zh_CN", url);
        Assert.Contains("story_review_table.json", url);
    }

    [Fact]
    public void UpsertSyncState_InsertsAndUpdates()
    {
        var svc = new StorySyncService();

        svc.UpsertSyncState("zh_CN", "abc123");
        var state = svc.GetSyncState("zh_CN");
        Assert.NotNull(state);
        Assert.Equal("abc123", state.LastCommitSha);

        svc.UpsertSyncState("zh_CN", "def456");
        state = svc.GetSyncState("zh_CN");
        Assert.Equal("def456", state.LastCommitSha);
    }

    [Fact]
    public void GetSyncState_NotFound_ReturnsNull()
    {
        var svc = new StorySyncService();
        var state = svc.GetSyncState("nonexistent");
        Assert.Null(state);
    }

    [Fact]
    public void UpdatePrtsSyncSha_UpdatesExistingRecord()
    {
        var svc = new StorySyncService();
        svc.UpsertSyncState("zh_CN", "abc");
        svc.UpdatePrtsSyncSha("zh_CN", "prts_sha_1");

        var state = svc.GetSyncState("zh_CN");
        Assert.NotNull(state);
        Assert.Equal("prts_sha_1", state.PrtsSyncedAtSha);
    }

    [Fact]
    public void GetActsFromDb_Empty_ReturnsEmpty()
    {
        var svc = new StorySyncService();
        var acts = svc.GetActsFromDb("zh_CN");
        Assert.Empty(acts);
    }

    [Fact]
    public void GetChaptersByActId_Empty_ReturnsEmpty()
    {
        var svc = new StorySyncService();
        var chapters = svc.GetChaptersByActId(999);
        Assert.Empty(chapters);
    }

    [Fact]
    public void GetActsByType_Empty_ReturnsEmpty()
    {
        var svc = new StorySyncService();
        var acts = svc.GetActsByType("zh_CN", "ACTIVITY_STORY");
        Assert.Empty(acts);
    }
}

[Collection("DbTests")]
public class StorySyncServiceWithPrefixDbTests : IDisposable
{
    private readonly string? _prefixDbPath;
    private readonly string? _tempDbPath;

    public StorySyncServiceWithPrefixDbTests()
    {
        var srcPath = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(srcPath))
        {
            _prefixDbPath = srcPath;
            // Copy to temp to avoid modifying the original
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"arkplot_prefix_{Guid.NewGuid():N}.db");
            File.Copy(srcPath, _tempDbPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        DbFactory.Reset();
        if (_tempDbPath != null)
            try { File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public void PrefixDb_CanReadActs()
    {
        if (_tempDbPath == null) return;

        DbFactory.ConfigureForTesting($"Data Source={_tempDbPath}");
        var svc = new StorySyncService();
        var acts = svc.GetActsFromDb("zh_CN");
        Assert.NotEmpty(acts);
    }

    [Fact]
    public void PrefixDb_CanReadChapters()
    {
        if (_tempDbPath == null) return;

        DbFactory.ConfigureForTesting($"Data Source={_tempDbPath}");
        var svc = new StorySyncService();
        var acts = svc.GetActsFromDb("zh_CN");
        if (acts.Count == 0) return;

        var chapters = svc.GetChaptersByActId(acts[0].Id);
        Assert.NotEmpty(chapters);
    }

    [Fact]
    public void PrefixDb_HasActs()
    {
        if (_tempDbPath == null) return;

        DbFactory.ConfigureForTesting($"Data Source={_tempDbPath}");
        var svc = new StorySyncService();
        var acts = svc.GetActsFromDb("zh_CN");
        Assert.True(acts.Count > 0, "Prefix DB should contain at least one act");
    }

    [Fact]
    public void PrefixDb_CanFilterByType()
    {
        if (_tempDbPath == null) return;

        DbFactory.ConfigureForTesting($"Data Source={_tempDbPath}");
        var svc = new StorySyncService();
        var allActs = svc.GetActsFromDb("zh_CN");
        if (allActs.Count == 0) return;

        var firstType = allActs[0].ActType;
        var filtered = svc.GetActsByType("zh_CN", firstType);
        Assert.NotEmpty(filtered);
        Assert.All(filtered, a => Assert.Equal(firstType, a.ActType));
    }
}
