using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class PortraitProcessorTests
{
    [Fact]
    public void IsPortrait_CharType_ReturnsTrue()
    {
        var entry = new ScriptLine { Type = "character" };
        Assert.True(PortraitProcessor.IsPortrait(entry));
    }

    [Fact]
    public void IsPortrait_CharslotType_ReturnsTrue()
    {
        var entry = new ScriptLine { Type = "charslot" };
        Assert.True(PortraitProcessor.IsPortrait(entry));
    }

    [Fact]
    public void IsPortrait_CharactercutinType_ReturnsTrue()
    {
        var entry = new ScriptLine { Type = "charactercutin" };
        Assert.True(PortraitProcessor.IsPortrait(entry));
    }

    [Fact]
    public void IsPortrait_DialogType_ReturnsFalse()
    {
        var entry = new ScriptLine { Type = "dialog" };
        Assert.False(PortraitProcessor.IsPortrait(entry));
    }

    [Fact]
    public void IsPortrait_BackgroundType_ReturnsFalse()
    {
        var entry = new ScriptLine { Type = "background" };
        Assert.False(PortraitProcessor.IsPortrait(entry));
    }

    [Fact]
    public void DetectPortraits_NoPortraits_EmptyList()
    {
        var lines = new List<ScriptLine>
        {
            new() { Index = 0, Type = "dialog", MdText = "hello" },
        };
        var ctx = new StoryDocumentContext(lines);
        ctx.Groups.Add(lines);

        PortraitProcessor.DetectPortraits(ctx);
        Assert.Empty(ctx.Portraits);
    }

    [Fact]
    public void DetectPortraits_WithPortrait_Detects()
    {
        var lines = new List<ScriptLine>
        {
            new() { Index = 0, Type = "charslot", MdText = "<img src=\"portrait.png\">", CharacterName = "阿米娅" },
            new() { Index = 1, Type = "dialog", MdText = "**阿米娅** 你好", CharacterName = "阿米娅" },
        };
        var ctx = new StoryDocumentContext(lines);
        ctx.Groups.Add(lines);

        PortraitProcessor.DetectPortraits(ctx);
        Assert.Single(ctx.Portraits);
    }

    [Fact]
    public void RemovePortraitLines_ClearsMdText()
    {
        var lines = new List<ScriptLine>
        {
            new() { Index = 0, Type = "dialog", MdText = "keep" },
            new() { Index = 1, Type = "charslot", MdText = "remove" },
        };
        var ctx = new StoryDocumentContext(lines);
        ctx.PortraitIndexes.Add(1);

        PortraitProcessor.RemovePortraitLines(ctx);

        Assert.Equal("keep", lines[0].MdText);
        Assert.Equal("", lines[1].MdText);
    }
}

[Collection("DbTests")]
public class StoryDocumentContextTests
{
    [Fact]
    public void Constructor_DefaultEnablesDescriptions()
    {
        var ctx = new StoryDocumentContext(new List<ScriptLine>());
        Assert.True(ctx.EnableDescriptions);
    }

    [Fact]
    public void Constructor_DisableDescriptions()
    {
        var ctx = new StoryDocumentContext(new List<ScriptLine>(), enableDescriptions: false);
        Assert.False(ctx.EnableDescriptions);
    }

    [Fact]
    public void Constructor_InitializesEmptyCollections()
    {
        var ctx = new StoryDocumentContext(new List<ScriptLine>());
        Assert.Empty(ctx.Groups);
        Assert.Empty(ctx.Portraits);
        Assert.Empty(ctx.PortraitIndexes);
        Assert.Empty(ctx.DescribedCharacters);
        Assert.Empty(ctx.DescribedImages);
    }
}
