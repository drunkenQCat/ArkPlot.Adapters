using System;
using System.IO;
using System.Linq;
using System.Text;
using ArkPlot.Arknights;
using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
using ArkPlot.Core.Infrastructure;
using Xunit;

namespace ArkPlot.Arknights.Tests;

/// <summary>
/// 回归测试：验证 charslot 的 action（如 shake）动作指令不会移除对应槽位的立绘。
///
/// 旧 bug：`[charslot(slot="l", action="shake", focus="l")]` 这类纯动作指令（无 name）被
/// GetCurrentPortraitsFromSlot 误判为"移除立绘"，导致后续对话行的 Portraits 只剩另一槽位的立绘，
/// focus 锚点把立绘归属误判给错误角色。
/// </summary>
[Collection("DbTests")]
public class CharslotShakeActionRegressionTests : IDisposable
{
    private readonly string? _dbPath;

    public CharslotShakeActionRegressionTests()
    {
        var prefixDb = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(prefixDb))
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_shake_regression_{Guid.NewGuid():N}.db");
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

    private static FormattedTextEntry ParseLine(PrtsPreloader preloader, string text, int index)
    {
        var entry = new FormattedTextEntry { Index = index, OriginalText = text };
        preloader.ParseOriginalText(entry);
        return entry;
    }

    /// <summary>
    /// 核心验证：双人立绘（slot=l 和 slot=r）后插入 shake 动作指令，
    /// 后续对话行的 Portraits 应同时保留两个立绘，且 focus 指向正确角色。
    /// </summary>
    [Fact]
    public void ShakeAction_KeepsSlotPortrait_ForSubsequentDialog()
    {
        if (!HasDb) return;

        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");

        var script = new StringBuilder(string.Join("\n", new[]
        {
            "[charslot(slot=\"l\",name=\"avg_220_grani_1\")]",
            "[charslot(slot=\"r\",name=\"char_130_doberm_ex\")]",
            "[charslot(slot=\"l\",name=\"avg_220_grani_1\",focus=\"l\")]",
            "[charslot(slot=\"l\",action=\"shake\",power=\"6\",times=\"40\",duration=\"0.3\",focus=\"l\")]",
            "[name=\"格拉尼\"]我在这里。",
        }));

        var plotManager = new PlotManager("shake_test", script);
        var textVariants = SplitEntries(script.ToString());
        plotManager.CurrentPlot.TextVariants = textVariants.Cast<ArkPlot.Core.Model.ScriptLine>().ToList();
        var preloader = new PrtsPreloader(plotManager);

        var entries = textVariants;
        foreach (var e in entries)
            preloader.ParseOriginalText(e);

        // 最后一行是格拉尼对话，应同时包含 l 槽位（grani）和 r 槽位（doberm）立绘
        var dialog = entries.Last();
        Assert.Equal("格拉尼", dialog.CharacterName);
        Assert.Contains("grani", string.Join(" ", dialog.Portraits));
        Assert.Contains("doberm", string.Join(" ", dialog.Portraits));
    }

    private static List<FormattedTextEntry> SplitEntries(string raw)
    {
        return raw.Split('\n', StringSplitOptions.None)
            .Select((line, i) => new FormattedTextEntry { Index = i, OriginalText = line.TrimEnd('\r') })
            .ToList();
    }
}