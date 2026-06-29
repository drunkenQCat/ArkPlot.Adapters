using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class StoryDocumentBuilderTests
{
    private static FormattedTextEntry MakeDialog(string name, string dialog, int index = 0)
    {
        return new FormattedTextEntry
        {
            Index = index,
            MdText = $"**{name}** {dialog}",
            Type = "dialog",
            CharacterName = name,
            Dialog = dialog,
            OriginalText = $"[name=\"{name}\"]{dialog}"
        };
    }

    private static FormattedTextEntry MakeBg(string url, int index = 0)
    {
        return new FormattedTextEntry
        {
            Index = index,
            MdText = $"![bg]({url})",
            Type = "background",
            OriginalText = $"[background(image=\"{url}\")]"
        };
    }

    [Fact]
    public void Builder_EmptyEntries_ReturnsEmptyResult()
    {
        var builder = new StoryDocumentBuilder(new List<FormattedTextEntry>());
        Assert.True(string.IsNullOrWhiteSpace(builder.Result));
    }

    [Fact]
    public void Builder_DialogEntries_ProducesOutput()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("阿米娅", "博士，我们出发吧。", 0),
            MakeDialog("博士", "好的。", 1),
        };

        var builder = new StoryDocumentBuilder(entries);
        var result = builder.Result;

        Assert.Contains("阿米娅", result);
        Assert.Contains("博士", result);
    }

    [Fact]
    public void Builder_RemovesEmptyEntries()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("A", "有内容", 0),
            new() { Index = 1, MdText = "", Type = "empty", OriginalText = "" },
            MakeDialog("B", "也有内容", 2),
        };

        var builder = new StoryDocumentBuilder(entries);
        Assert.Contains("有内容", builder.Result);
    }

    [Fact]
    public void Builder_NormalizesStageDirections()
    {
        var entries = new List<FormattedTextEntry>
        {
            new() { Index = 0, MdText = "`瞳孔地震`", Type = "dialog", OriginalText = "test" },
        };

        var builder = new StoryDocumentBuilder(entries);
        Assert.Contains("`震惊`", builder.Result);
        Assert.DoesNotContain("`瞳孔地震`", builder.Result);
    }

    [Fact]
    public void Builder_NormalizesImagePan()
    {
        var entries = new List<FormattedTextEntry>
        {
            new() { Index = 0, MdText = "`图像平移`", Type = "dialog", OriginalText = "test" },
        };

        var builder = new StoryDocumentBuilder(entries);
        Assert.Contains("`场景流转`", builder.Result);
    }

    [Fact]
    public void Builder_ReadableMode_UsesReadableRenderer()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("角色", "对话内容", 0),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.Readable);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_PromptMode_UsesPromptRenderer()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("角色", "对话内容", 0),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_WithSeparator_SplitsGroups()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("A", "第一段", 0),
            new() { Index = 1, MdText = "---", Type = "separator", OriginalText = "[delay]" },
            MakeDialog("B", "第二段", 2),
        };

        var builder = new StoryDocumentBuilder(entries);
        var result = builder.Result;

        Assert.Contains("第一段", result);
        Assert.Contains("第二段", result);
    }

    [Fact]
    public void Builder_AppendResultToBuilder_AppendsToStringBuilder()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("X", "append测试", 0),
        };

        var sb = new System.Text.StringBuilder();
        sb.Append("前缀");
        var builder = new StoryDocumentBuilder(entries);
        builder.AppendResultToBuilder(sb);

        var result = sb.ToString();
        Assert.StartsWith("前缀", result);
        Assert.Contains("append测试", result);
    }

    [Fact]
    public void Builder_WithPicDesc_EnablesDescriptions()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("角色", "对话", 0),
        };
        entries[0].PicDesc = "角色描述文本";

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: true);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_DisableDescriptions()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeDialog("角色", "对话", 0),
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false);
        Assert.NotNull(builder.Result);
    }
}
