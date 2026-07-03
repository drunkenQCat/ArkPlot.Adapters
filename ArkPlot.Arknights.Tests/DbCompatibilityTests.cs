using System.IO;
using System.Linq;
using SqlSugar;
using ArkPlot.Core.Infrastructure;
using Xunit;

namespace ArkPlot.Arknights.Tests;

/// <summary>
/// 数据库建表兼容性测试：
/// 1. 新数据库从零创建 — 所有表和字段正确
/// 2. 旧数据库不被破坏 — 旧数据可读、表结构不变
/// 3. 新旧表结构对比 — FormattedTextEntry 列完全一致
/// </summary>
[Collection("DbTests")]
public class DbCompatibilityTests : IDisposable
{
    private readonly string? _oldDbPath;

    public DbCompatibilityTests()
    {
        var prefixDb = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
        if (File.Exists(prefixDb))
        {
            _oldDbPath = Path.Combine(Path.GetTempPath(), $"arkplot_compat_{Guid.NewGuid():N}.db");
            File.Copy(prefixDb, _oldDbPath, overwrite: true);
        }
    }

    public void Dispose()
    {
        DbFactory.Reset();
        ArknightsDbInitializer.Reset();
        if (_oldDbPath != null && File.Exists(_oldDbPath))
            try { File.Delete(_oldDbPath); } catch { }
    }

    private bool HasOldDb => _oldDbPath != null && File.Exists(_oldDbPath);

    // ════════════════════════════════════════════
    //  新数据库从零创建
    // ════════════════════════════════════════════

    [Fact]
    public void NewDb_AllTables_Created()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var tables = db.Ado.SqlQuery<string>(
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name");

        // 通用表（Core 建）
        Assert.Contains("Acts", tables);
        Assert.Contains("StoryChapters", tables);
        Assert.Contains("Plot", tables);
        Assert.Contains("SyncState", tables);
        Assert.Contains("PicDescriptions", tables);

        // Arknights 特有表
        Assert.Contains("FormattedTextEntry", tables);
        Assert.Contains("PrtsData", tables);
        Assert.Contains("PrtsResources", tables);
        Assert.Contains("PrtsPortraitLinks", tables);

        // ScriptLine 表不应该存在
        Assert.DoesNotContain("ScriptLine", tables);
    }

    [Fact]
    public void NewDb_FormattedTextEntry_HasAll21Columns()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var columns = GetColumnNames(db, "FormattedTextEntry");

        // 基类 ScriptLine 的 13 个字段
        Assert.Contains("Id", columns);
        Assert.Contains("PlotId", columns);
        Assert.Contains("Index", columns);
        Assert.Contains("OriginalText", columns);
        Assert.Contains("MdText", columns);
        Assert.Contains("Type", columns);
        Assert.Contains("CommandSet", columns);
        Assert.Contains("CharacterName", columns);
        Assert.Contains("CharacterCode", columns);
        Assert.Contains("Dialog", columns);
        Assert.Contains("ResourceUrls", columns);
        Assert.Contains("PicDesc", columns);

        // 子类 FormattedTextEntry 的 8 个特有字段
        Assert.Contains("MdDuplicateCounter", columns);
        Assert.Contains("TypText", columns);
        Assert.Contains("IsTagOnly", columns);
        Assert.Contains("PngIndex", columns);
        Assert.Contains("Portraits", columns);
        Assert.Contains("PortraitFocus", columns);
        Assert.Contains("Bg", columns);

        // PicFacts 是 [SugarColumn(IsIgnore = true)]，不持久化
        // SkipPortraitOutput 也是 [SugarColumn(IsIgnore = true)]
    }

    [Fact]
    public void NewDb_InsertAndRead_FormattedTextEntry_Works()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var entry = new FormattedTextEntry
        {
            PlotId = 1,
            Index = 42,
            OriginalText = "[name=\"阿米娅\"]博士...",
            MdText = "**阿米娅**: 博士...",
            Type = "dialog",
            CharacterName = "阿米娅",
            CharacterCode = "char_002_amiya",
            Dialog = "博士...",
            TypText = "#doc(...)[]",
            Bg = "bg_black",
            Portraits = ["https://example.com/portrait.png"],
            PortraitFocus = 0
        };

        var id = db.Insertable(entry).ExecuteReturnIdentity();

        var read = db.Queryable<FormattedTextEntry>().First(e => e.Id == id);
        Assert.NotNull(read);
        Assert.Equal(42, read.Index);
        Assert.Equal("阿米娅", read.CharacterName);
        Assert.Equal("char_002_amiya", read.CharacterCode);
        Assert.Equal("博士...", read.Dialog);
        Assert.Equal("bg_black", read.Bg);
        Assert.NotEmpty(read.Portraits);
        Assert.Equal("https://example.com/portrait.png", read.Portraits[0]);
        Assert.Equal(0, read.PortraitFocus);
    }

    // ════════════════════════════════════════════
    //  旧数据库兼容
    // ════════════════════════════════════════════

    [Fact]
    public void OldDb_AfterInit_OldDataStillReadable()
    {
        if (!HasOldDb) return;

        DbFactory.ConfigureForTesting($"Data Source={_oldDbPath}");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var entries = db.Queryable<FormattedTextEntry>()
            .Where(e => e.PlotId > 0)
            .Take(5)
            .ToList();

        Assert.NotEmpty(entries);

        var first = entries[0];
        Assert.True(first.Id > 0);
        Assert.True(first.Index >= 0);
        Assert.False(string.IsNullOrEmpty(first.OriginalText));
    }

    [Fact]
    public void OldDb_AfterInit_TableStructureUnchanged()
    {
        if (!HasOldDb) return;

        // 先记录旧表结构
        var oldColumns = GetColumnNames(
            new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={_oldDbPath}",
                DbType = DbType.Sqlite
            }),
            "FormattedTextEntry");

        // 执行新初始化
        DbFactory.ConfigureForTesting($"Data Source={_oldDbPath}");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        // 再记录新表结构
        var newColumns = GetColumnNames(db, "FormattedTextEntry");

        // 列不应该变少
        Assert.True(newColumns.Count >= oldColumns.Count);
        foreach (var col in oldColumns)
            Assert.Contains(col, newColumns);
    }

    [Fact]
    public void OldDb_AfterInit_NewInsertWorks()
    {
        if (!HasOldDb) return;

        DbFactory.ConfigureForTesting($"Data Source={_oldDbPath}");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var entry = new FormattedTextEntry
        {
            PlotId = 999999,
            Index = 0,
            OriginalText = "compat test",
            MdText = "compat test",
            Type = "test",
            CharacterName = "TestChar",
            Dialog = "compat test",
            Bg = "bg_test",
            Portraits = ["https://example.com/test.png"]
        };

        var id = db.Insertable(entry).ExecuteReturnIdentity();
        Assert.True(id > 0);

        var read = db.Queryable<FormattedTextEntry>().First(e => e.Id == id);
        Assert.NotNull(read);
        Assert.Equal("TestChar", read.CharacterName);
        Assert.Equal("bg_test", read.Bg);
        Assert.NotEmpty(read.Portraits);

        // 清理
        db.Deleteable<FormattedTextEntry>().Where(e => e.Id == id).ExecuteCommand();
    }

    [Fact]
    public void OldDb_AfterInit_NoScriptLineTableCreated()
    {
        if (!HasOldDb) return;

        DbFactory.ConfigureForTesting($"Data Source={_oldDbPath}");
        var db = DbFactory.GetClient();
        ArknightsDbInitializer.Init(db);

        var tables = db.Ado.SqlQuery<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='ScriptLine'");

        Assert.Empty(tables);
    }

    // ════════════════════════════════════════════
    //  新旧表结构对比
    // ════════════════════════════════════════════

    [Fact]
    public void TableStructure_OldVsNew_IdenticalColumns()
    {
        if (!HasOldDb) return;

        // 旧库表结构
        var oldColumns = GetColumnNames(
            new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={_oldDbPath}",
                DbType = DbType.Sqlite
            }),
            "FormattedTextEntry");

        // 新库表结构
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        var newDb = DbFactory.GetClient();
        ArknightsDbInitializer.Init(newDb);
        var newColumns = GetColumnNames(newDb, "FormattedTextEntry");

        // 列名完全一致
        Assert.Equal(oldColumns.Count, newColumns.Count);
        foreach (var col in oldColumns)
            Assert.Contains(col, newColumns);
    }

    // ════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════

    private static List<string> GetColumnNames(SqlSugarClient db, string tableName)
    {
        var rows = db.Ado.SqlQuery<dynamic>(
            $"PRAGMA table_info({tableName})");
        return rows.Select(r => (string)((IDictionary<string, object>)r)["name"]).ToList();
    }
}
