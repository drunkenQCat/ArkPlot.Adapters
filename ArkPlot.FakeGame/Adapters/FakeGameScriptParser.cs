using System.Text.Json;
using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;

namespace ArkPlot.FakeGame;

/// <summary>
/// 假假游戏脚本解析器：将结构化 JSON 对话数据解析为 ScriptLine。
/// 假设输入 JSON 格式：
/// [
///   {"type": "dialog", "speaker": "Alice", "speaker_code": "char_alice", "text": "你好"},
///   {"type": "bg_change", "value": "forest"},
///   {"type": "narration", "text": "夜幕降临了。"}
/// ]
/// </summary>
public class FakeGameScriptParser : IScriptParser
{
    public string GameId => "fake-game";

    public List<ScriptLine> Parse(string rawText, string chapterTitle)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return new List<ScriptLine>();

        var entries = new List<ScriptLine>();
        var nodes = JsonDocument.Parse(rawText).RootElement.EnumerateArray().ToList();

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var line = new ScriptLine { Index = i };

            var type = node.GetProperty("type").GetString() ?? "";
            line.Type = MapType(type);
            line.OriginalText = node.GetRawText();

            // 解析对话
            if (node.TryGetProperty("text", out var text))
                line.Dialog = text.GetString() ?? "";

            // 解析说话人
            if (node.TryGetProperty("speaker", out var speaker))
                line.CharacterName = speaker.GetString() ?? "";

            // 解析角色代码
            if (node.TryGetProperty("speaker_code", out var code))
                line.CharacterCode = code.GetString();

            // 解析资源值（背景、音乐等）
            if (node.TryGetProperty("value", out var val))
            {
                var resourceUrl = ResolveResource(type, val.GetString() ?? "");
                line.ResourceUrls.Add(resourceUrl);
            }

            entries.Add(line);
        }

        return entries;
    }

    public HashSet<ResourceRef> CollectResources(List<ScriptLine> lines)
    {
        var resources = new HashSet<ResourceRef>();
        foreach (var line in lines)
        {
            foreach (var url in line.ResourceUrls)
            {
                var resourceType = line.Type switch
                {
                    "portrait" => "portrait",
                    "background" => "background",
                    "audio" => "audio",
                    _ => "image"
                };
                resources.Add(new ResourceRef(resourceType, url));
            }
        }
        return resources;
    }

    /// <summary>将假假游戏的原始类型名映射为通用类型名。</summary>
    private static string MapType(string rawType)
    {
        return rawType switch
        {
            "dialog" or "char_show" => "dialog",
            "narration" => "narration",
            "bg_change" or "background" => "background",
            "portrait" or "char_show" => "portrait",
            "play_music" or "music" => "audio",
            "play_sound" or "sfx" => "audio",
            _ => rawType
        };
    }

    /// <summary>将资源代号转换为 URL（占位实现，实际应查映射表）。</summary>
    private static string ResolveResource(string type, string value)
    {
        return type switch
        {
            "bg_change" or "background" => $"https://fakegame.example.com/bg/{value}.png",
            "play_music" or "music" => $"https://fakegame.example.com/bgm/{value}.ogg",
            "play_sound" or "sfx" => $"https://fakegame.example.com/sfx/{value}.ogg",
            _ => $"https://fakegame.example.com/res/{value}.png"
        };
    }
}
