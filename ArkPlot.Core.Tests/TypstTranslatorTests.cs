using ArkPlot.Core.Utilities.TypstComponents;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class TypstTranslatorTests
{
    [Fact]
    public void Constructor_SetsChapterName()
    {
        var t = new TypstTranslator("测试章节");
        Assert.Equal("测试章节", t.ChapterName);
    }

    [Fact]
    public void TypCode_StartsWithImport()
    {
        var t = new TypstTranslator("test");
        Assert.Contains("arknights_sim", t.TypCode);
    }

    [Fact]
    public void SetName_And_UpdateCode_ContainsName()
    {
        var t = new TypstTranslator("ch1");
        t.SetName("\"阿米娅\"");
        t.SetScript("\"你好\"");
        t.SetPortrait("\"portrait.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        Assert.Contains("\"阿米娅\"", t.TypCode);
    }

    [Fact]
    public void UpdateCode_WithoutPortrait2_UsesSinglePortrait()
    {
        var t = new TypstTranslator("ch1");
        t.SetName("\"name\"");
        t.SetScript("\"script\"");
        t.SetPortrait("\"p.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        Assert.Contains("arknights_sim(", t.TypCode);
        Assert.DoesNotContain("arknights_sim_2p", t.TypCode);
    }

    [Fact]
    public void UpdateCode_WithPortrait2_UsesTwoPortraits()
    {
        var t = new TypstTranslator("ch1");
        t.SetName("\"name\"");
        t.SetScript("\"script\"");
        t.SetPortrait("\"p1.png\"");
        t.SetPortrait2("\"p2.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        Assert.Contains("arknights_sim_2p", t.TypCode);
    }

    [Fact]
    public void UpdateCode_ClearsNameAndScript()
    {
        var t = new TypstTranslator("ch1");
        t.SetName("\"name1\"");
        t.SetScript("\"script1\"");
        t.SetPortrait("\"p.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        // Second update with new values
        t.SetName("\"name2\"");
        t.SetScript("\"script2\"");
        t.SetPortrait("\"p.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        Assert.Contains("name1", t.TypCode);
        Assert.Contains("name2", t.TypCode);
    }

    [Fact]
    public void MultipleUpdates_Accumulate()
    {
        var t = new TypstTranslator("ch1");
        t.SetName("\"A\"");
        t.SetScript("\"s1\"");
        t.SetPortrait("\"p.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        t.SetName("\"B\"");
        t.SetScript("\"s2\"");
        t.SetPortrait("\"p.png\"");
        t.SetBackground("\"bg.png\"");
        t.UpdateCode();

        Assert.Contains("\"A\"", t.TypCode);
        Assert.Contains("\"B\"", t.TypCode);
    }
}
