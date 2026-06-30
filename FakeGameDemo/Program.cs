using System.Text;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.FakeGame;
using Microsoft.Data.Sqlite;

// ──── 1. 初始化数据库 ────
var dbPath = Path.Combine(AppContext.BaseDirectory, "fakegame_demo.db");
if (File.Exists(dbPath)) File.Delete(dbPath);

DbFactory.ConfigureForTesting($"Data Source={dbPath}");
var db = DbFactory.GetClient();

Console.WriteLine($"数据库: {dbPath}");
Console.WriteLine();

// ──── 2. 解析 JSON 并写入数据库 ────
var json = @"[
  {""type"": ""bg_change"", ""value"": ""forest""},
  {""type"": ""play_music"", ""value"": ""calm_theme""},
  {""type"": ""dialog"", ""speaker"": ""Alice"", ""speaker_code"": ""char_alice"", ""text"": ""欢迎来到这片森林。""},
  {""type"": ""dialog"", ""speaker"": ""Bob"", ""speaker_code"": ""char_bob"", ""text"": ""这里真美……空气中都是树的味道。""},
  {""type"": ""narration"", ""text"": ""两人沿着小径慢慢走着。阳光从树叶间洒下来。""},
  {""type"": ""dialog"", ""speaker"": ""Alice"", ""speaker_code"": ""char_alice"", ""text"": ""前面有个休息点，我们先停一下吧。""},
  {""type"": ""dialog"", ""speaker"": ""Bob"", ""speaker_code"": ""char_bob"", ""text"": ""好，正好我也累了。""}
]";

var parser = new FakeGameScriptParser();
var lines = parser.Parse(json, "森林之旅");

var renderer = new FakeGameTagRenderer();
foreach (var line in lines)
{
    if (!string.IsNullOrEmpty(line.Dialog))
        line.MdText = renderer.RenderLine(line);
}

var plot = new Plot("森林之旅", new StringBuilder())
{
    ActId = 1,
    StoryChapterId = 1
};
plot.TextVariants = lines;

await PlotCache<ScriptLine>.SaveAsync(plot, lines, PlotStatus.Parsed);

Console.WriteLine("=== 写入完成，从数据库读回 ===");
var loaded = await PlotCache<ScriptLine>.TryLoadAsync(1, "森林之旅");
if (loaded != null)
{
    foreach (var line in loaded.Value.Entries)
    {
        Console.WriteLine($"  [{line.Index}] Type={line.Type,-12} Speaker={line.CharacterName,-8} Dialog={line.Dialog}");
    }
}

// ──── 3. 检查数据库表结构 ────
Console.WriteLine();
Console.WriteLine("=== 数据库表结构 ===");
using (var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly"))
{
    conn.Open();
    using var cmdTables = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name", conn);
    using var reader = cmdTables.ExecuteReader();
    var tables = new List<string>();
    while (reader.Read()) tables.Add(reader.GetString(0));

    foreach (var table in tables)
    {
        using var cmdSchema = new SqliteCommand($"PRAGMA table_info({table})", conn);
        using var r = cmdSchema.ExecuteReader();
        var cols = new List<string>();
        while (r.Read()) cols.Add($"  {r.GetString(1)} ({r.GetString(2)})");
        Console.WriteLine($"\n{table}:");
        Console.WriteLine(string.Join("\n", cols));
    }

    Console.WriteLine("\n=== ScriptLine 表数据 ===");
    using var cmdData = new SqliteCommand("SELECT Id, PlotId, [Index], Type, CharacterName, Dialog FROM ScriptLine ORDER BY [Index]", conn);
    using var r2 = cmdData.ExecuteReader();
    while (r2.Read())
    {
        Console.WriteLine($"  [{r2.GetInt32(2)}] Type={r2.GetString(3),-12} Speaker={r2.GetString(4),-8} Dialog={r2.GetString(5)}");
    }
}

DbFactory.Reset();
