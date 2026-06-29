using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using ArkPlot.FakeGame;

var json = @"[
  {""type"": ""bg_change"", ""value"": ""forest""},
  {""type"": ""play_music"", ""value"": ""calm_theme""},
  {""type"": ""dialog"", ""speaker"": ""Alice"", ""speaker_code"": ""char_alice"", ""text"": ""欢迎来到这片森林。""},
  {""type"": ""dialog"", ""speaker"": ""Bob"", ""speaker_code"": ""char_bob"", ""text"": ""这里真美……空气中都是树的味道。""},
  {""type"": ""narration"", ""text"": ""两人沿着小径慢慢走着。阳光从树叶间洒下来。""},
  {""type"": ""dialog"", ""speaker"": ""Alice"", ""speaker_code"": ""char_alice"", ""text"": ""前面有个休息点，我们先停一下吧。""},
  {""type"": ""dialog"", ""speaker"": ""Bob"", ""speaker_code"": ""char_bob"", ""text"": ""好，正好我也累了。""}
]";

Console.WriteLine("=== 输入 JSON ===");
Console.WriteLine(json);
Console.WriteLine();

var parser = new FakeGameScriptParser();
var lines = parser.Parse(json, "森林之旅");

Console.WriteLine("=== 解析后 ScriptLine ===");
foreach (var line in lines)
{
    Console.WriteLine($"  [{line.Index}] Type={line.Type,-12} Speaker={line.CharacterName,-8} Dialog={line.Dialog}");
}
Console.WriteLine();

var renderer = new FakeGameTagRenderer();
foreach (var line in lines.OfType<FormattedTextEntry>())
{
    if (!string.IsNullOrEmpty(line.Dialog))
        line.MdText = renderer.RenderLine(line);
}

var builder = new StoryDocumentBuilder(lines.OfType<FormattedTextEntry>().ToList());

Console.WriteLine("=== 输出 Markdown ===");
Console.WriteLine(builder.Result);
