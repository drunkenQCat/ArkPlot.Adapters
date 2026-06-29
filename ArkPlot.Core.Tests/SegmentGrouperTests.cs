using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class SegmentGrouperTests
{
    private static FormattedTextEntry MakeEntry(string mdText, string type = "dialog", int index = 0)
    {
        return new FormattedTextEntry { MdText = mdText, Type = type, Index = index };
    }

    [Fact]
    public void Group_EmptyList_ReturnsEmpty()
    {
        var result = SegmentGrouper.Group(new());
        Assert.Empty(result);
    }

    [Fact]
    public void Group_SingleEntry_ReturnsSingleGroup()
    {
        var entries = new List<FormattedTextEntry> { MakeEntry("hello") };
        var result = SegmentGrouper.Group(entries);
        Assert.Single(result);
        Assert.Single(result[0]);
    }

    [Fact]
    public void Group_ExplicitSeparator_SplitsGroups()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeEntry("line1"),
            MakeEntry("---"),
            MakeEntry("line2"),
        };
        var result = SegmentGrouper.Group(entries);
        Assert.Equal(2, result.Count);
        Assert.Single(result[0]);
        Assert.Single(result[1]);
    }

    [Fact]
    public void Group_PlaymusicSceneChange_SplitsGroups()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeEntry("scene1 dialogue"),
            MakeEntry("bgm_switch", type: "playmusic"),
            MakeEntry("scene2 dialogue"),
        };
        var result = SegmentGrouper.Group(entries);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Group_LeadingDashesInGroup_AreCleared()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeEntry("---"),
            MakeEntry("content"),
        };
        var result = SegmentGrouper.Group(entries);
        Assert.Single(result);
        // The "---" line should be in the first group but its MdText cleared
        Assert.All(result[0], e => Assert.NotEqual("---", e.MdText));
    }

    [Fact]
    public void Group_SmallSegment_NoImplicitSplit()
    {
        // Less than MinSegmentSize (16), even with dash-starting lines, no implicit split
        var entries = Enumerable.Range(0, 10)
            .Select(i => MakeEntry(i == 5 ? "-separator" : $"line{i}", index: i))
            .ToList();

        var result = SegmentGrouper.Group(entries);
        Assert.Single(result);
    }

    [Fact]
    public void Group_LargeSegment_ImplicitSplitAtDash()
    {
        // 20 lines + dash-starting line triggers split
        var entries = new List<FormattedTextEntry>();
        for (int i = 0; i < 20; i++)
            entries.Add(MakeEntry($"line{i}", index: i));
        entries.Add(MakeEntry("-new section", index: 20));
        entries.Add(MakeEntry("continuation", index: 21));

        var result = SegmentGrouper.Group(entries);
        Assert.Equal(2, result.Count);
        Assert.Equal(20, result[0].Count);
    }

    [Fact]
    public void Group_MultipleExplicitSeparators_CreateMultipleGroups()
    {
        var entries = new List<FormattedTextEntry>
        {
            MakeEntry("a"),
            MakeEntry("---"),
            MakeEntry("b"),
            MakeEntry("---"),
            MakeEntry("c"),
        };
        var result = SegmentGrouper.Group(entries);
        Assert.Equal(3, result.Count);
    }
}
