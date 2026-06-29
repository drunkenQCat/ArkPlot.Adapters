using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Arknights;

using ArkPlot.Arknights.Adapters;
namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class ArknightsTagRendererTests
{
    [Fact]
    public void RequiresRulesFile_IsTrue()
    {
        var renderer = new ArknightsTagRenderer();
        Assert.True(renderer.RequiresRulesFile);
    }

    [Fact]
    public void RenderLine_EmptyOriginalText_ReturnsEmpty()
    {
        var renderer = new ArknightsTagRenderer();
        var line = new FormattedTextEntry { OriginalText = "" };
        var result = renderer.RenderLine(line);
        Assert.Equal("", result);
    }

    [Fact]
    public void RenderLine_ScriptLine_ReturnsOriginalText()
    {
        var renderer = new ArknightsTagRenderer();
        var line = new ScriptLine { OriginalText = "plain text" };
        var result = renderer.RenderLine(line);
        Assert.Equal("plain text", result);
    }
}
