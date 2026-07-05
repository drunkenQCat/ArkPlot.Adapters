using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArkPlot.Arknights.Data;
using ArkPlot.Core.Infrastructure;
using SqlSugar;
using Xunit;

namespace ArkPlot.Arknights.Tests;

/// <summary>
/// Benchmark：对比内存查找 vs ORM 查询在完整活动构建过程中的性能差异。
/// 
/// 方式 A — 内存查找（当前）：PrtsAssets.Instance 加载后，GetPortraitUrl 从 PortraitLinkDocument 查
/// 方式 B — ORM 查询（无索引）：Queryable<PrtsPortraitLink> 查 DB，CharacterCode 无索引
/// 方式 C — ORM 查询 + 实例缓存：首次查 DB 后缓存到实例，后续走内存
/// </summary>
[Collection("DbTests")]
public class PortraitLookupBenchmark : IDisposable
{
    private readonly string? _dbPath;

    public PortraitLookupBenchmark()
    {
        var prefixDb = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(prefixDb))
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_bench_{Guid.NewGuid():N}.db");
            File.Copy(prefixDb, _dbPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        DbFactory.Reset();
        if (_dbPath != null && File.Exists(_dbPath))
            try { File.Delete(_dbPath); } catch { }
    }

    private bool HasDb => _dbPath != null && File.Exists(_dbPath);

    /// <summary>
    /// 从 FormattedTextEntry 表中提取骑黑与猎人的真实角色 code 序列，
    /// 模拟 PrtsPreloader 调用 GetPortraitUrl 时的 inputKey 序列。
    /// </summary>
    private List<string> ExtractRealCharacterKeys()
    {
        var db = DbFactory.GetClient();
        var entries = db.Queryable<ArkPlot.Core.Model.Plot>()
            .Where(p => p.Title != null && p.Title.Contains("骑兵与猎人"))
            .ToList();

        var plotIds = entries.Select(p => p.Id).ToList();
        if (plotIds.Count == 0)
        {
            // fallback: 取所有条目
            var allEntries = db.Queryable<FormattedTextEntry>()
                .Where(e => e.PlotId > 0)
                .Take(500)
                .Select(e => e.OriginalText)
                .ToList();
            return ExtractCharCodes(allEntries);
        }

        var texts = db.Queryable<FormattedTextEntry>()
            .Where(e => plotIds.Contains(e.PlotId))
            .OrderBy(e => e.Index)
            .Select(e => e.OriginalText)
            .ToList();

        return ExtractCharCodes(texts);
    }

    private static List<string> ExtractCharCodes(List<string> texts)
    {
        var keys = new List<string>();
        var regex = new Regex(@"(?:name|Character\(name)=""([^""]+)""",
            RegexOptions.Compiled);

        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text)) continue;
            var matches = regex.Matches(text);
            foreach (Match m in matches)
            {
                if (m.Groups[1].Success)
                    keys.Add(m.Groups[1].Value);
            }
        }

        return keys.Count > 0 ? keys : new List<string>
        {
            "char_220_grani", "avg_npc_003", "char_263_skadi",
            "avg_npc_009", "avg_npc_008", "avg_npc_007",
            "char_003_kalts_1", "char_143_ghost", "char_148_nearl_1"
        };
    }

    [Fact]
    public async Task Benchmark_Memory_vs_ORM_vs_ORMCached()
    {
        if (!HasDb) return;

        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
        var keys = ExtractRealCharacterKeys();

        // ──── 方式 A：内存查找（当前方式）────
        // 先 EnsureSyncedAsync 加载到内存
        var prtsA = new PrtsDataProcessor();
        await prtsA.EnsureSyncedAsync();

        var swA = Stopwatch.StartNew();
        var resultsA = new List<string>();
        foreach (var key in keys)
        {
            var url = prtsA.GetPortraitUrl(key);
            resultsA.Add(url);
        }
        swA.Stop();

        // ──── 方式 B：ORM 查询（无索引）────
        var db = DbFactory.GetClient();
        var swB = Stopwatch.StartNew();
        var resultsB = new List<string>();
        foreach (var key in keys)
        {
            // 模拟 GetPortraitUrl 的核心查找：先查 PortraitLink，再查 Resource
            var (charCode, index) = prtsA.GetCharacterCode(key) != null
                ? (prtsA.GetCharacterCode(key)!, 0)
                : (key, 0);

            var links = db.Queryable<PrtsPortraitLink>()
                .Where(l => l.CharacterCode == charCode)
                .OrderBy(l => l.SortOrder)
                .ToList();

            string urlB;
            if (links.Count > 0 && index < links.Count)
            {
                var portraitName = links[index].PortraitName;
                var resource = db.Queryable<PrtsResource>()
                    .Where(r => r.ResourceType == "Char" && r.ResourceKey == portraitName)
                    .First();
                urlB = resource?.ResourceUrl ?? "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";
            }
            else
            {
                urlB = "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";
            }
            resultsB.Add(urlB);
        }
        swB.Stop();

        // ──── 方式 C：ORM 查询 + 实例缓存 ────
        var cache = new Dictionary<string, string>();
        var linkCache = new Dictionary<string, List<PrtsPortraitLink>>();
        var swC = Stopwatch.StartNew();
        var resultsC = new List<string>();
        foreach (var key in keys)
        {
            var (charCode, index) = prtsA.GetCharacterCode(key) != null
                ? (prtsA.GetCharacterCode(key)!, 0)
                : (key, 0);

            if (!linkCache.TryGetValue(charCode, out var links))
            {
                links = db.Queryable<PrtsPortraitLink>()
                    .Where(l => l.CharacterCode == charCode)
                    .OrderBy(l => l.SortOrder)
                    .ToList();
                linkCache[charCode] = links;
            }

            string urlC;
            if (links.Count > 0 && index < links.Count)
            {
                var portraitName = links[index].PortraitName;
                if (!cache.TryGetValue(portraitName, out urlC))
                {
                    var resource = db.Queryable<PrtsResource>()
                        .Where(r => r.ResourceType == "Char" && r.ResourceKey == portraitName)
                        .First();
                    urlC = resource?.ResourceUrl ?? "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";
                    cache[portraitName] = urlC;
                }
            }
            else
            {
                urlC = "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";
            }
            resultsC.Add(urlC);
        }
        swC.Stop();

        // ──── 生成报告 ────
        var report = $@"# Portrait Lookup Benchmark 报告

## 测试数据

- 数据库: {_dbPath}
- 角色代码提取来源: 骑兵与猎人活动 FormattedTextEntry
- 调用次数: {keys.Count} 次 GetPortraitUrl
- 去重角色数: {keys.Distinct().Count()} 个

## 结果

| 方式 | 总耗时 | 平均每次 | 说明 |
|------|--------|---------|------|
| A. 内存查找（当前） | {swA.ElapsedMilliseconds} ms | {swA.Elapsed.TotalMilliseconds / keys.Count:F3} ms | PrtsAssets.Instance 单例 + JSON Document |
| B. ORM 查询（无索引） | {swB.ElapsedMilliseconds} ms | {swB.Elapsed.TotalMilliseconds / keys.Count:F3} ms | Queryable<PrtsPortraitLink> + Queryable<PrtsResource> |
| C. ORM + 实例缓存 | {swC.ElapsedMilliseconds} ms | {swC.Elapsed.TotalMilliseconds / keys.Count:F3} ms | 首次查 DB，后续走实例缓存 |

## 对比

| 对比 | 差值 | 倍数 |
|------|------|------|
| B vs A | +{swB.ElapsedMilliseconds - swA.ElapsedMilliseconds} ms | {swB.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x |
| C vs A | +{swC.ElapsedMilliseconds - swA.ElapsedMilliseconds} ms | {swC.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x |

## 索引状态

- PrtsResource: 有唯一索引 (ResourceType, ResourceKey)
- PrtsPortraitLink: **无 CharacterCode 索引**（只有主键 Id）

## 结论

- ORM 无索引方式比内存查找慢 {swB.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x
- ORM + 缓存方式比内存查找慢 {swC.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x
- 如果在 CharacterCode 上加索引，ORM 查询性能预计可提升（SQLite 索引查找 vs 全表扫描）
";

        var reportPath = Path.Combine(Path.GetTempPath(), "portrait_lookup_benchmark.md");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"Benchmark report: {reportPath}");
        Console.WriteLine($"A (memory):     {swA.ElapsedMilliseconds} ms ({swA.Elapsed.TotalMilliseconds / keys.Count:F3} ms/call)");
        Console.WriteLine($"B (ORM no idx): {swB.ElapsedMilliseconds} ms ({swB.Elapsed.TotalMilliseconds / keys.Count:F3} ms/call)");
        Console.WriteLine($"C (ORM cached): {swC.ElapsedMilliseconds} ms ({swC.Elapsed.TotalMilliseconds / keys.Count:F3} ms/call)");
        Console.WriteLine($"B/A ratio: {swB.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x");
        Console.WriteLine($"C/A ratio: {swC.Elapsed.TotalMilliseconds / swA.Elapsed.TotalMilliseconds:F1}x");

        // 确保结果一致（不是 thorns fallback）
        Assert.DoesNotContain("thorns", string.Join(",", resultsA));
        Assert.True(resultsA.Count == keys.Count);
    }
}
