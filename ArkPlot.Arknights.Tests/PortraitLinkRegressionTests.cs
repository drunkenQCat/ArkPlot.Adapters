using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
using ArkPlot.Core.Infrastructure;
using Xunit;

namespace ArkPlot.Arknights.Tests;

/// <summary>
/// 回归测试：验证 PrtsAssets.Instance 单例修复后，
/// PrtsPreloader（内部 new PrtsDataProcessor()）能访问到 EnsureSyncedAsync 加载的数据。
/// 旧 bug：所有角色 fallback 到 thorns 立绘，音效/背景链接全失。
/// </summary>
[Collection("DbTests")]
public class PortraitLinkRegressionTests : IDisposable
{
    private readonly string? _dbPath;

    public PortraitLinkRegressionTests()
    {
        var prefixDb = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(prefixDb))
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_portrait_regression_{Guid.NewGuid():N}.db");
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

    private async Task SetupPrtsData()
    {
        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
        var prts = new PrtsDataProcessor();
        await prts.EnsureSyncedAsync();
    }

    /// <summary>
    /// 核心验证：EnsureSyncedAsync 加载后，new PrtsDataProcessor() 能访问到同一份数据。
    /// </summary>
    [Fact]
    public async Task Singleton_DataShared_BetweenPrtsDataProcessorInstances()
    {
        if (!HasDb) return;

        await SetupPrtsData();

        // 模拟 PrtsPreloader 内部 new PrtsDataProcessor()
        var internalPrts = new PrtsDataProcessor();

        Assert.True(internalPrts.Res.DataChar.Count > 0, "DataChar should be loaded via singleton");
        Assert.True(internalPrts.Res.DataImage.Count > 0, "DataImage should be loaded via singleton");
        Assert.True(internalPrts.Res.DataAudio.Count > 0, "DataAudio should be loaded via singleton");

        // PortraitLinkDocument 不应该是空 "{}"
        var docJson = internalPrts.Res.PortraitLinkDocument.RootElement.ToString();
        Assert.NotEqual("{}", docJson);
        Assert.True(docJson.Length > 100, "PortraitLinkDocument should have content");
    }

    /// <summary>
    /// 验证 GetPortraitUrl 不返回 thorns fallback URL。
    /// </summary>
    [Theory]
    [InlineData("char_220_grani")]
    [InlineData("avg_npc_003")]
    [InlineData("char_263_skadi")]
    public async Task GetPortraitUrl_RealCharacter_NotThornsFallback(string charCode)
    {
        if (!HasDb) return;

        await SetupPrtsData();

        // 模拟 PrtsPreloader 内部 new PrtsDataProcessor()
        var internalPrts = new PrtsDataProcessor();
        var url = internalPrts.GetPortraitUrl(charCode);

        Assert.DoesNotContain("thorns", url);
        Assert.Contains("prts.wiki", url);
    }

    /// <summary>
    /// 端到端验证：PrtsPreloader 解析真实剧本片段，
    /// 生成资源链接报告 MD 文件，验证不含 thorns fallback。
    /// </summary>
    [Fact]
    public async Task PrtsPreloader_ResourceReport_NoThornsFallback()
    {
        if (!HasDb) return;

        await SetupPrtsData();

        var script = new StringBuilder(string.Join("\n", new[]
        {
            "[HEADER(key=\"title_test\", is_skippable=true, fit_mode=\"BLACK_MASK\")] 回归测试章节",
            "[Background(image=\"bg_caveentrance\", fadetime=1)]",
            "[PlayMusic(intro=\"$m_dia_street_intro\", key=\"$m_dia_street_loop\", volume=0.6,crossfade=1)]",
            "[name=\"char_220_grani\"] 我是格拉尼，骑黑与猎人的骑士。",
            "[name=\"avg_npc_003\"] 你好，格拉尼。",
            "[Character(name=\"char_263_skadi\")] 斯卡蒂来了。",
        }));

        var plotManager = new PlotManager("regression_test", script);
        plotManager.InitializePlot();

        var preloader = new PrtsPreloader(plotManager);
        preloader.ParseAndCollectAssets();

        // 生成资源链接报告 MD
        var sb = new StringBuilder();
        sb.AppendLine("# Portrait Link 回归测试报告\n");
        sb.AppendLine($"- 测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- PrtsAssets.Instance.DataChar 条目数: {PrtsAssets.Instance.DataChar.Count}");
        sb.AppendLine($"- PrtsAssets.Instance.DataImage 条目数: {PrtsAssets.Instance.DataImage.Count}");
        sb.AppendLine($"- PrtsAssets.Instance.DataAudio 条目数: {PrtsAssets.Instance.DataAudio.Count}");
        sb.AppendLine($"- PortraitLinkDocument 大小: {PrtsAssets.Instance.PortraitLinkDocument.RootElement.ToString().Length} chars\n");
        sb.AppendLine("## 解析结果\n");

        var entries = plotManager.CurrentPlot.TextVariants;
        int thornsCount = 0;
        int totalPortraits = 0;
        int totalResourceUrls = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var fte = entry as FormattedTextEntry;
            sb.AppendLine($"### Entry {i}: Type={entry.Type}\n");
            sb.AppendLine($"- OriginalText: `{entry.OriginalText?.Truncate(60)}`");
            sb.AppendLine($"- CharacterName: {entry.CharacterName ?? "(null)"}");
            sb.AppendLine($"- CharacterCode: {entry.CharacterCode ?? "(null)"}");
            sb.AppendLine($"- Dialog: `{entry.Dialog?.Truncate(40)}`");

            if (fte != null)
            {
                sb.AppendLine($"- Bg: `{fte.Bg ?? "(empty)"}'");
                if (fte.Portraits.Count > 0)
                {
                    totalPortraits += fte.Portraits.Count;
                    foreach (var p in fte.Portraits)
                    {
                        sb.AppendLine($"  - Portrait: `{p}`");
                        if (p.Contains("thorns")) thornsCount++;
                    }
                }
            }

            if (entry.ResourceUrls.Count > 0)
            {
                totalResourceUrls += entry.ResourceUrls.Count;
                foreach (var url in entry.ResourceUrls)
                {
                    sb.AppendLine($"  - ResourceUrl: `{url}`");
                    if (url.Contains("thorns")) thornsCount++;
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 统计\n");
        sb.AppendLine($"- 总条目数: {entries.Count}");
        sb.AppendLine($"- 总 Portraits 数: {totalPortraits}");
        sb.AppendLine($"- 总 ResourceUrls 数: {totalResourceUrls}");
        sb.AppendLine($"- thorns fallback 出现次数: {thornsCount}");

        var reportPath = Path.Combine(Path.GetTempPath(), "portrait_regression_report.md");
        File.WriteAllText(reportPath, sb.ToString());
        Console.WriteLine($"Report written to: {reportPath}");
        Console.WriteLine($"thorns fallback count: {thornsCount}");

        // 核心断言：thorns fallback 不应该出现
        Assert.Equal(0, thornsCount);

        // 至少有一些 Portraits 或 ResourceUrls
        Assert.True(totalPortraits > 0 || totalResourceUrls > 0,
            "Should have at least some portraits or resource URLs");
    }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        string.IsNullOrEmpty(s) ? "(empty)" : (s.Length <= max ? s : s[..max] + "…");
}
