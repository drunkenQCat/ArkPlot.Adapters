using ArkPlot.Core.Utilities;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class StringExtensionsTests
{
    [Fact]
    public void ToCommandSet_SinglePair()
    {
        var result = "name=测试".ToCommandSet();
        Assert.Single(result);
        Assert.Equal("测试", result["name"]);
    }

    [Fact]
    public void ToCommandSet_MultiplePairs()
    {
        var result = "name=测试,width=100,height=200".ToCommandSet();
        Assert.Equal(3, result.Count);
        Assert.Equal("测试", result["name"]);
        Assert.Equal("100", result["width"]);
        Assert.Equal("200", result["height"]);
    }

    [Fact]
    public void ToCommandSet_QuotedValue()
    {
        var result = "name=\"角色A\",type=dialog".ToCommandSet();
        Assert.Equal("角色A", result["name"]);
        Assert.Equal("dialog", result["type"]);
    }

    [Fact]
    public void ToCommandSet_KeysAreLowercased()
    {
        var result = "NAME=测试,Type=dialog".ToCommandSet(isToLower: true);
        Assert.True(result.ContainsKey("name"));
        Assert.True(result.ContainsKey("type"));
    }

    [Fact]
    public void ToCommandSet_KeysPreserveCase_WhenNotToLower()
    {
        var result = "Name=测试".ToCommandSet(isToLower: false);
        Assert.True(result.ContainsKey("Name"));
    }

    [Fact]
    public void ToCommandSet_EmptyInput_ReturnsEmpty()
    {
        var result = "".ToCommandSet();
        Assert.Empty(result);
    }

    [Fact]
    public void ToCommandSet_DuplicateKeys_TakesFirst()
    {
        var result = "name=A,name=B".ToCommandSet();
        Assert.Equal("A", result["name"]);
    }

    [Fact]
    public void GetValue_ReturnsPartAfterSeparator()
    {
        Assert.Equal("value", "key:value".GetValue(":"));
    }

    [Fact]
    public void GetValue_NoSeparator_ReturnsEmpty()
    {
        Assert.Equal("", "noseparator".GetValue(":"));
    }

    [Fact]
    public void GetValue_MultipleSeparators_TakesLast()
    {
        Assert.Equal("c", "a:b:c".GetValue(":"));
    }

    [Fact]
    public void GetKey_ReturnsPartBeforeSeparator()
    {
        Assert.Equal("key", "key:value".GetKey(":"));
    }

    [Fact]
    public void GetKey_NoSeparator_ReturnsWholeString()
    {
        Assert.Equal("noseparator", "noseparator".GetKey(":"));
    }

    [Fact]
    public void GetKey_MultipleSeparators_TakesBeforeLast()
    {
        Assert.Equal("a:b", "a:b:c".GetKey(":"));
    }

    [Fact]
    public void ToArray_SplitsByNewline()
    {
        var result = "a\nb\nc".ToArray("\n");
        Assert.Equal(3, result.Length);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("c", result[2]);
    }

    [Fact]
    public void ToArray_StripsCarriageReturn()
    {
        var result = "a\rb\rc".ToArray("\r");
        Assert.All(result, s => Assert.DoesNotContain("\r", s));
    }

    [Fact]
    public void ToArray_NoMatch_ReturnsSingleElement()
    {
        var result = "hello".ToArray("|");
        Assert.Single(result);
        Assert.Equal("hello", result[0]);
    }

    [Fact]
    public void ToArray_SplitsByPipe()
    {
        var result = "x|y|z".ToArray("|");
        Assert.Equal(3, result.Length);
    }
}
