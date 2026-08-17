using System.IO;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class PicDescServiceTests : IDisposable
{
    private readonly string _dbPath;

    public PicDescServiceTests()
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
    public async Task GetOrCreatePicDescAsync_SkipCache_DoesNotWriteToDb()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult("[Mock描述] 假描述，不应入库"),
            skipCache: true);
        var result = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/test.png");

        Assert.Contains("[Mock描述]", result);

        // Mock 描述不应写入数据库缓存
        var db = DbFactory.GetClient();
        var count = db.Queryable<PicDescription>()
            .Count(it => it.ImageUrl == "https://media.prts.wiki/test.png");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_SkipCache_IgnoresExistingDbCache()
    {
        // 数据库已有真实缓存（模拟之前真实 API 生成过的数据）
        var db = DbFactory.GetClient();
        db.Insertable(new PicDescription
        {
            DedupKey = "https://media.prts.wiki/existing.png",
            ImageUrl = "https://media.prts.wiki/existing.png",
            PicDesc = "来自真实 API 的旧缓存",
            PicFacts = "旧事实",
            Source = "Vision",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteCommand();

        // Mock 模式下 skipCache=true：必须无视 DB 已有缓存，强制重新生成（触发描述流程/日志）
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult("[Mock描述] 假描述，用于调试日志");
            },
            skipCache: true);

        var result = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/existing.png");

        // 描述委托被调用 → 日志流程能输出；结果来自 Mock 而非 DB 旧缓存
        Assert.Equal(1, callCount);
        Assert.Equal("[Mock描述] 假描述，用于调试日志", result);

        // 旧缓存未被覆盖（skipCache 不写库）
        var record = db.Queryable<PicDescription>()
            .First(it => it.ImageUrl == "https://media.prts.wiki/existing.png");
        Assert.Equal("来自真实 API 的旧缓存", record.PicDesc);
    }

    [Fact]
    public async Task GetOrCreatePicDescWithFactsAsync_SkipCache_IgnoresExistingDbCache()
    {
        // 数据库已有按 DedupKey 缓存的记录；Mock 模式下也必须强制重新生成
        var db = DbFactory.GetClient();
        db.Insertable(new PicDescription
        {
            DedupKey = "char_debug",
            ImageUrl = "https://media.prts.wiki/char_debug.png",
            PicDesc = "旧缓存描述",
            PicFacts = "旧事实",
            Source = "Vision",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteCommand();

        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult("[Mock描述] 新生成的调试描述");
            },
            skipCache: true);

        var result = await svc.GetOrCreatePicDescWithFactsAsync(
            "https://media.prts.wiki/char_debug.png", "char_debug");

        Assert.Equal(1, callCount);
        Assert.Equal("[Mock描述] 新生成的调试描述", result.Description);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_NonImageUrl_ReturnsEmpty()
    {
        using var svc = new PicDescService();
        var result = await svc.GetOrCreatePicDescAsync("https://example.com/audio.mp3");
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_EmptyUrl_ReturnsEmpty()
    {
        using var svc = new PicDescService();
        var result = await svc.GetOrCreatePicDescAsync("");
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_SkipUrls_ReturnsEmpty()
    {
        using var svc = new PicDescService();
        var result = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/8/8a/Avg_bg_bg_black.png");
        Assert.Equal("", result);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_WithMockDelegate_ReturnsDescription()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult($"描述: {url}"));
        var result = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/test.png");
        Assert.Contains("描述:", result);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_NoDelegate_ReturnsPlaceholder()
    {
        using var svc = new PicDescService();
        var result = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/scenes/bg_forest.png");
        Assert.StartsWith("[PIC_DESC:", result);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_CachesResult_InDb()
    {
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult("缓存测试描述");
            });

        var result1 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/cache_test.png");
        var result2 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/cache_test.png");

        Assert.Equal("缓存测试描述", result1);
        Assert.Equal("缓存测试描述", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_CharacterCode_DedupByCode()
    {
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult($"角色描述_{callCount}");
            });

        var r1 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/char_a_1.png", "char_001");
        var r2 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/char_a_2.png", "char_001");

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_CharacterCodeWithHash_StripsHash()
    {
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult("描述");
            });

        await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/char_1.png", "char_002#3");
        await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/char_2.png", "char_002");

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreatePicDescWithFactsAsync_ExtractsFacts()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult("银色长发，蓝色眼睛的角色"),
            extractFacts: desc => Task.FromResult("hair: [银色, 长发]\nfeatures: [蓝色眼睛]"));

        var result = await svc.GetOrCreatePicDescWithFactsAsync("https://media.prts.wiki/char.png", "char_test");

        Assert.Equal("银色长发，蓝色眼睛的角色", result.Description);
        Assert.Contains("hair:", result.Facts);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_DebugMode_SkipsCacheAndRegenerates()
    {
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult($"描述_{callCount}");
            },
            debugMode: true);

        var result1 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/debug.png");
        var result2 = await svc.GetOrCreatePicDescAsync("https://media.prts.wiki/debug.png");

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetOrCreatePicDescsAsync_BatchFiltersNonImages()
    {
        using var svc = new PicDescService(
            describeByUrl: url => Task.FromResult("描述"));

        var urls = new[]
        {
            "https://media.prts.wiki/bg.png",
            "https://media.prts.wiki/audio.mp3",
            "https://media.prts.wiki/scene.jpg",
        };
        var result = await svc.GetOrCreatePicDescsAsync(urls);

        Assert.Equal("", result["https://media.prts.wiki/audio.mp3"]);
        Assert.NotEqual("", result["https://media.prts.wiki/bg.png"]);
        Assert.NotEqual("", result["https://media.prts.wiki/scene.jpg"]);
    }

    [Fact]
    public void CleanNonImageRecords_RemovesAudioUrls()
    {
        var db = DbFactory.GetClient();
        db.Insertable(new PicDescription
        {
            DedupKey = "audio_key",
            ImageUrl = "https://media.prts.wiki/audio.mp3",
            PicDesc = "test",
            Source = "Vision",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteCommand();

        using var svc = new PicDescService();
        var deleted = svc.CleanNonImageRecords();

        Assert.Equal(1, deleted);
        var remaining = db.Queryable<PicDescription>().Count();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void GetStats_ReturnsCorrectCounts()
    {
        var db = DbFactory.GetClient();
        db.Insertable(new PicDescription
        {
            DedupKey = "key1",
            ImageUrl = "https://media.prts.wiki/test.png",
            PicDesc = "desc",
            Source = "Vision",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteCommand();

        using var svc = new PicDescService();
        var (dbCount, _, _) = svc.GetStats();

        Assert.Equal(1, dbCount);
    }

    [Fact]
    public async Task GetOrCreatePicDescAsync_TwoLevelLookup_FallbackToUrl()
    {
        var callCount = 0;
        using var svc = new PicDescService(
            describeByUrl: url =>
            {
                callCount++;
                return Task.FromResult("共享描述");
            });

        await svc.GetOrCreatePicDescWithFactsAsync("https://media.prts.wiki/shared.png", "char_A");
        var result = await svc.GetOrCreatePicDescWithFactsAsync("https://media.prts.wiki/shared.png", "char_B");

        Assert.Equal(1, callCount);
        Assert.Equal("共享描述", result.Description);
    }
}
