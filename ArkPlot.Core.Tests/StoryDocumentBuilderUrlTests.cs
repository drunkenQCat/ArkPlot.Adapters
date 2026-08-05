using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

public class StoryDocumentBuilderUrlTests
{
    [Fact]
    public void ReadableMode_KeepsImageUrlInMdText()
    {
        const string url = "https://example.com/background.png";
        var entries = new List<ScriptLine>
        {
            new()
            {
                Index = 0,
                MdText = $"<img src=\"{url}\" alt=\"bg\" loading=\"lazy\" style=\"max-height:350px\"/>\r\n\r\n`背景`：bg",
                Type = "background",
                OriginalText = "[background(image=\"bg\")]",
                ResourceUrls = [url]
            },
            new()
            {
                Index = 1,
                MdText = "**博士** 出发吧。",
                Type = "dialog",
                CharacterName = "博士",
                Dialog = "出发吧。",
                OriginalText = "[name=\"博士\"]出发吧。"
            }
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false, outputMode: OutputMode.Readable);
        var result = builder.Result;

        Assert.Contains(url, result);
    }

    [Fact]
    public void ReadableMode_KeepsAudioUrlInMdText()
    {
        const string url = "https://example.com/music.mp3";
        var entries = new List<ScriptLine>
        {
            new()
            {
                Index = 0,
                MdText = $"<audio controls class=\"music\" width=\"300\" alt=\"music\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>\r\n\r\n`音乐`：music",
                Type = "playmusic",
                OriginalText = "[playmusic(key=\"music\")]",
                ResourceUrls = [url]
            }
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false, outputMode: OutputMode.Readable);
        var result = builder.Result;

        Assert.Contains(url, result);
    }

    [Fact]
    public void ReadableMode_PortraitRow_GeneratesTableWithUrl()
    {
        const string url = "https://example.com/portrait.png";
        var entries = new List<ScriptLine>
        {
            new()
            {
                Index = 0,
                MdText = $"<img class=\"portrait\" src=\"{url}\" alt=\"avg_amiya_1\" loading=\"lazy\" style=\"max-height:300px\"/>",
                Type = "character",
                CharacterName = "阿米娅",
                CharacterCode = "amiya",
                OriginalText = "[character(name=\"阿米娅\")]",
                ResourceUrls = [url]
            },
            new()
            {
                Index = 1,
                MdText = "**阿米娅** 博士。",
                Type = "dialog",
                CharacterName = "阿米娅",
                Dialog = "博士。",
                OriginalText = "[name=\"阿米娅\"]博士。"
            }
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false, outputMode: OutputMode.Readable);
        var result = builder.Result;

        Assert.Contains(url, result);
    }

    [Fact]
    public void ReadableMode_PortraitRow_WithoutFocus_FallsBackToFirstUrl()
    {
        const string url = "https://example.com/portrait_no_focus.png";
        var entries = new List<ScriptLine>
        {
            new()
            {
                Index = 0,
                MdText = $"<img class=\"portrait\" src=\"{url}\" alt=\"avg_amiya_1\" loading=\"lazy\" style=\"max-height:300px\"/>",
                Type = "character",
                CharacterName = "阿米娅",
                CharacterCode = "amiya",
                OriginalText = "[character(name=\"阿米娅\")]",
                ResourceUrls = [url],
                CommandSet = new StringDict { { "type", "character" } }
            },
            new()
            {
                Index = 1,
                MdText = "**阿米娅** 博士。",
                Type = "dialog",
                CharacterName = "阿米娅",
                Dialog = "博士。",
                OriginalText = "[name=\"阿米娅\"]博士。"
            }
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false, outputMode: OutputMode.Readable);
        var result = builder.Result;

        Assert.Contains(url, result);
    }

    [Fact]
    public void PromptMode_PortraitOnlyCharacter_EmitsPortraitFacts()
    {
        const string url = "https://example.com/portrait.png";
        const string facts = "hair: [黑色, 短发]\nclothing: [外套]";
        var entries = new List<ScriptLine>
        {
            new()
            {
                Index = 0,
                MdText = $"<img class=\"portrait\" src=\"{url}\" alt=\"avg_npc_999\" loading=\"lazy\" style=\"max-height:300px\"/>",
                Type = "character",
                CharacterName = "路人甲",
                CharacterCode = "npc_999",
                OriginalText = "[character(name=\"路人甲\")]",
                ResourceUrls = [url],
                PicFacts = facts
            },
            new()
            {
                Index = 1,
                MdText = "旁白描述，没有路人甲的台词。",
                Type = "narrator",
                OriginalText = "旁白描述，没有路人甲的台词。"
            }
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: true, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.Contains("<aside class=\"portrait-facts\"", result);
        Assert.Contains(facts, result);
    }
}
