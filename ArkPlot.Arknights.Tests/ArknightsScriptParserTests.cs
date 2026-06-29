using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Arknights;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class ArknightsScriptParserTests
{
    private readonly ArknightsScriptParser _parser = new();

    [Fact]
    public void GameId_IsArknights()
    {
        Assert.Equal("arknights", _parser.GameId);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmpty()
    {
        var result = _parser.Parse("", "测试");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SimpleDialog_ReturnsSingleLine()
    {
        var result = _parser.Parse("这是一句旁白", "测试");
        Assert.Single(result);
        Assert.Equal("这是一句旁白", result[0].Dialog);
    }

    [Fact]
    public void Parse_MultipleLines_PreservesOrder()
    {
        var input = "第一行\n第二行\n第三行";
        var result = _parser.Parse(input, "测试");
        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].Index);
        Assert.Equal(1, result[1].Index);
        Assert.Equal(2, result[2].Index);
    }

    [Fact]
    public void Parse_WithNameTag_ExtractsCharacterName()
    {
        var input = "[name=\"阿米娅\"]博士，准备好了吗？";
        var result = _parser.Parse(input, "测试");

        Assert.Single(result);
        Assert.Equal("阿米娅", result[0].CharacterName);
        Assert.Equal("博士，准备好了吗？", result[0].Dialog);
    }

    [Fact]
    public void Parse_CommentLines_AreSkipped()
    {
        var input = "//这是注释\n正常内容";
        var result = _parser.Parse(input, "测试");

        Assert.Equal(2, result.Count);
        Assert.Equal("", result[0].Dialog);
        Assert.Equal("正常内容", result[1].Dialog);
    }

    [Fact]
    public void Parse_WhitespaceLines_AreSkipped()
    {
        var input = "   \n正常内容";
        var result = _parser.Parse(input, "测试");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CollectResources_EmptyLines_ReturnsEmpty()
    {
        var lines = new List<ScriptLine>();
        var result = _parser.CollectResources(lines);
        Assert.Empty(result);
    }

    [Fact]
    public void CollectResources_DialogLine_NoResources()
    {
        var lines = new List<ScriptLine>
        {
            new FormattedTextEntry { Type = "dialog", Dialog = "对话" }
        };
        var result = _parser.CollectResources(lines);
        Assert.Empty(result);
    }

    [Fact]
    public void CollectResources_PortraitLine_CollectsPortraitUrl()
    {
        var lines = new List<ScriptLine>
        {
            new FormattedTextEntry
            {
                Type = "charslot",
                ResourceUrls = ["https://media.prts.wiki/char.png"]
            }
        };
        var result = _parser.CollectResources(lines);
        Assert.Single(result);
        Assert.Contains(result, r => r.ResourceType == "portrait");
    }

    [Fact]
    public void CollectResources_BackgroundLine_CollectsBackgroundUrl()
    {
        var lines = new List<ScriptLine>
        {
            new FormattedTextEntry
            {
                Type = "background",
                ResourceUrls = ["https://media.prts.wiki/bg.png"]
            }
        };
        var result = _parser.CollectResources(lines);
        Assert.Single(result);
        Assert.Contains(result, r => r.ResourceType == "background");
    }

    [Fact]
    public void CollectResources_DuplicateUrls_Deduped()
    {
        var lines = new List<ScriptLine>
        {
            new FormattedTextEntry
            {
                Type = "charslot",
                ResourceUrls = ["https://media.prts.wiki/char.png"]
            },
            new FormattedTextEntry
            {
                Type = "charslot",
                ResourceUrls = ["https://media.prts.wiki/char.png"]
            }
        };
        var result = _parser.CollectResources(lines);
        Assert.Single(result);
    }
}
