using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class StoryDocumentBuilderTests
{
    private static ScriptLine MakeDialog(string name, string dialog, int index = 0)
    {
        return new ScriptLine
        {
            Index = index,
            MdText = $"**{name}** {dialog}",
            Type = "dialog",
            CharacterName = name,
            Dialog = dialog,
            OriginalText = $"[name=\"{name}\"]{dialog}"
        };
    }

    private static ScriptLine MakeBg(string url, int index = 0)
    {
        return new ScriptLine
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
        var builder = new StoryDocumentBuilder(new List<ScriptLine>());
        Assert.True(string.IsNullOrWhiteSpace(builder.Result));
    }

    [Fact]
    public void Builder_DialogEntries_ProducesOutput()
    {
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, MdText = "`图像平移`", Type = "dialog", OriginalText = "test" },
        };

        var builder = new StoryDocumentBuilder(entries);
        Assert.Contains("`场景流转`", builder.Result);
    }

    [Fact]
    public void Builder_ReadableMode_UsesReadableRenderer()
    {
        var entries = new List<ScriptLine>
        {
            MakeDialog("角色", "对话内容", 0),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.Readable);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_PromptMode_UsesPromptRenderer()
    {
        var entries = new List<ScriptLine>
        {
            MakeDialog("角色", "对话内容", 0),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_WithSeparator_SplitsGroups()
    {
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
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
        var entries = new List<ScriptLine>
        {
            MakeDialog("角色", "对话", 0),
        };

        var builder = new StoryDocumentBuilder(entries, enableDescriptions: false);
        Assert.NotNull(builder.Result);
    }

    [Fact]
    public void Builder_PromptMode_DecisionOption_BecomesPlayerLine()
    {
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, MdText = "`选项`呼啊......;......;嘶......谁把窗帘拉开的？", Type = "decision", OriginalText = "[Decision(options=\"呼啊......;......;嘶......谁把窗帘拉开的？\")]" },
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        // 必须归属到博士，且不再是无主的 `选项` 标签
        Assert.Contains("**博士**（玩家可选台词）：呼啊......;......;嘶......谁把窗帘拉开的？", result);
        Assert.DoesNotContain("`选项`呼啊", result);
    }

    [Fact]
    public void Builder_PromptMode_PredicateIndex_BecomesBranchComment()
    {
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, MdText = "`选项`1;2;3", Type = "predicate", OriginalText = "[Predicate(references=\"1;2;3\")]" },
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.Contains("[可选分支：1;2;3]（此处为博士/玩家讲话）", result);
    }

    [Fact]
    public void Builder_PromptMode_SingleDigitOption_BecomesBranchComment()
    {
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, MdText = "`选项`1", Type = "predicate", OriginalText = "[Predicate(references=\"1\")]" },
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.Contains("[可选分支：1]（此处为博士/玩家讲话）", result);
    }

    [Fact]
    public void Builder_PromptMode_ReadableMode_OptionUntouched()
    {
        // Readable 模式保持原样，不转换选项行
        var entries = new List<ScriptLine>
        {
            new() { Index = 0, MdText = "`选项`呼啊......;......;嘶......谁把窗帘拉开的？", Type = "decision", OriginalText = "[Decision(options=\"HTTP\")]" },
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.Readable);
        var result = builder.Result;

        Assert.Contains("`选项`呼啊......;......;嘶......谁把窗帘拉开的？", result);
        Assert.DoesNotContain("**博士**（玩家可选台词）", result);
    }

    [Fact]
    public void Builder_PromptMode_CrossName_LaterNameAnnotatesFirstName()
    {
        // 同一 CharacterCode 换名：首名行标「后文作」，后续名行标「即前文」
        var entries = new List<ScriptLine>
        {
            MakeDialogWithCode("精英打扮的男性", "char_427", "先生，您生病了。", 0),
            MakeDialogWithCode("小贾斯汀", "char_427", "您可以喊我小贾斯汀。", 1),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.Contains("**精英打扮的男性（后文作“小贾斯汀”）**`讲道：`", result);
        Assert.Contains("**小贾斯汀（即前文“精英打扮的男性”）**`讲道：`", result);
    }

    [Fact]
    public void Builder_PromptMode_CrossName_AsideListsAllNames()
    {
        var entries = new List<ScriptLine>
        {
            MakeDialogWithCode("精英打扮的男性", "char_427", "先生，您生病了。", 0, picFacts: "西装革履，领口别着紫金徽章。"),
            MakeDialogWithCode("小贾斯汀", "char_427", "您可以喊我小贾斯汀。", 1),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.Contains("data-character=\"精英打扮的男性（即同一角色）小贾斯汀\"", result);
    }

    [Fact]
    public void Builder_PromptMode_CrossName_SameNameNoAnnotation()
    {
        // 名字未变化 → 不加任何跨名提示
        var entries = new List<ScriptLine>
        {
            MakeDialogWithCode("缪尔赛思", "char_249", "又想见你了。", 0),
            MakeDialogWithCode("缪尔赛思", "char_249", "我来给你送早饭。", 1),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        Assert.DoesNotContain("（后文作", result);
        Assert.DoesNotContain("（即前文", result);
    }

    [Fact]
    public void Builder_PromptMode_CrossName_StripsRevealSuffixInAnnotation()
    {
        // 「？？？」占位名经 NormalizeDisplayNames 加工为「神秘人士（实际人物：可萝尔）」。
        // 跨名标注引用该名字时只能带原始显示名「神秘人士」，不能把揭示后缀拼进标注（否则嵌套）。
        var entries = new List<ScriptLine>
        {
            MakeDialogWithCode("神秘人士（实际人物：可萝尔）", "char_999", "……", 0),
            MakeDialogWithCode("可萝尔", "char_999", "你们好。", 1),
        };

        var builder = new StoryDocumentBuilder(entries, outputMode: OutputMode.PromptOptimized);
        var result = builder.Result;

        // 后文真实名行标注前文名时：只用原始显示名「神秘人士」
        Assert.Contains("**可萝尔（即前文“神秘人士”）**", result);
        Assert.DoesNotContain("（即前文“神秘人士（实际人物：可萝尔）”）", result);

        // 前文加工名行标注后文名时：后文名本无揭示后缀，原样引用
        Assert.Contains("**神秘人士（实际人物：可萝尔）（后文作“可萝尔”）**", result);
    }

    private static ScriptLine MakeDialogWithCode(string name, string code, string dialog, int index = 0, string picFacts = "")
    {
        var line = new ScriptLine
        {
            Index = index,
            MdText = $"**{name}**`讲道：`{dialog}",
            Type = "dialog",
            CharacterName = name,
            CharacterCode = code,
            Dialog = dialog,
            OriginalText = $"[name=\"{name}\"]{dialog}",
            PicFacts = picFacts,
        };
        return line;
    }
}
