using ArkPlot.Core.Utilities;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class HtmlTagParserTests
{
    [Fact]
    public void Parse_SimpleTag()
    {
        var parser = new HtmlTagParser("<img src=\"test.png\">");
        Assert.True(parser.Attributes.ContainsKey("src"));
        Assert.Equal("test.png", parser.Attributes["src"]);
    }

    [Fact]
    public void Parse_MultipleAttributes()
    {
        var parser = new HtmlTagParser("<img src=\"test.png\" alt=\"desc\" width=\"100\">");
        Assert.Equal(3, parser.Attributes.Count);
        Assert.Equal("test.png", parser.Attributes["src"]);
        Assert.Equal("desc", parser.Attributes["alt"]);
        Assert.Equal("100", parser.Attributes["width"]);
    }

    [Fact]
    public void Parse_NoAttributes()
    {
        var parser = new HtmlTagParser("<div>");
        Assert.Empty(parser.Attributes);
    }

    [Fact]
    public void ReconstructHtml_PreservesTagAndAttributes()
    {
        var parser = new HtmlTagParser("<img src=\"test.png\">");
        var html = parser.ReconstructHtml();
        Assert.StartsWith("<img", html);
        Assert.Contains("src=\"test.png\"", html);
        Assert.EndsWith(">", html);
    }

    [Fact]
    public void ReconstructHtml_WithModifiedAttributes()
    {
        var parser = new HtmlTagParser("<img src=\"old.png\">");
        parser.Attributes["src"] = "new.png";
        var html = parser.ReconstructHtml();
        Assert.Contains("src=\"new.png\"", html);
        Assert.DoesNotContain("old.png", html);
    }

    [Fact]
    public void ReconstructHtml_AddNewAttribute()
    {
        var parser = new HtmlTagParser("<img src=\"test.png\">");
        parser.Attributes["title"] = "hello";
        var html = parser.ReconstructHtml();
        Assert.Contains("title=\"hello\"", html);
    }

    [Fact]
    public void Parse_EmptyInput_NoAttributes()
    {
        var parser = new HtmlTagParser("");
        Assert.Empty(parser.Attributes);
    }
}
