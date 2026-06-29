using System.IO;
using ArkPlot.Core.Infrastructure;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class ImageCachePathsTests
{
    [Fact]
    public void GetRelativePathFromUrl_StripsProtocol()
    {
        var result = ImageCachePaths.GetRelativePathFromUrl("https://media.prts.wiki/8/82/Avg.png");
        Assert.Contains("media.prts.wiki", result);
        Assert.Contains("Avg.png", result);
    }

    [Fact]
    public void GetRelativePathFromUrl_HttpProtocol()
    {
        var result = ImageCachePaths.GetRelativePathFromUrl("http://example.com/path/to/file.png");
        Assert.Contains("example.com", result);
        Assert.Contains("file.png", result);
    }

    [Fact]
    public void IsNetworkUrl_Https()
    {
        Assert.True(ImageCachePaths.IsNetworkUrl("https://media.prts.wiki/8/82/Avg.png"));
    }

    [Fact]
    public void IsNetworkUrl_Http()
    {
        Assert.True(ImageCachePaths.IsNetworkUrl("http://example.com/file.png"));
    }

    [Fact]
    public void IsNetworkUrl_DomainSlash()
    {
        Assert.True(ImageCachePaths.IsNetworkUrl("media.prts.wiki/8/82/Avg.png"));
    }

    [Fact]
    public void IsNetworkUrl_LocalPath_ReturnsFalse()
    {
        Assert.False(ImageCachePaths.IsNetworkUrl("pics/image.png"));
    }

    [Fact]
    public void IsNetworkUrl_NoDotInFirstSegment_ReturnsFalse()
    {
        Assert.False(ImageCachePaths.IsNetworkUrl("local/path/file.png"));
    }

    [Fact]
    public void NormalizeUrl_WithProtocol_ReturnsUnchanged()
    {
        Assert.Equal("https://example.com/file.png", ImageCachePaths.NormalizeUrl("https://example.com/file.png"));
    }

    [Fact]
    public void NormalizeUrl_WithoutProtocol_AddsHttps()
    {
        Assert.Equal("https://media.prts.wiki/file.png", ImageCachePaths.NormalizeUrl("media.prts.wiki/file.png"));
    }

    [Fact]
    public void NormalizeUrl_NoSlash_ReturnsUnchanged()
    {
        Assert.Equal("localfile", ImageCachePaths.NormalizeUrl("localfile"));
    }

    [Fact]
    public void GetTypstRelativePath_UsesForwardSlashes()
    {
        var result = ImageCachePaths.GetTypstRelativePath("孤星", "https://media.prts.wiki/8/82/Avg.png");
        Assert.DoesNotContain("\\", result);
        Assert.Contains("media.prts.wiki", result);
    }

    [Fact]
    public void GetAbsolutePath_ContainsStoryName()
    {
        var result = ImageCachePaths.GetAbsolutePath("孤星", "https://media.prts.wiki/8/82/Avg.png");
        Assert.Contains("孤星", result);
    }
}

[Collection("DbTests")]
public class OutputPathsTests
{
    [Fact]
    public void ActRootRelative_ContainsStoryName()
    {
        var result = OutputPaths.ActRootRelative("测试活动");
        Assert.Contains("测试活动", result);
        Assert.StartsWith("output", result);
    }

    [Fact]
    public void ActRootAbsolute_IsAbsolutePath()
    {
        var result = OutputPaths.ActRootAbsolute("测试活动");
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void TtsDir_EndsWithTts()
    {
        var result = OutputPaths.TtsDir("活动");
        Assert.EndsWith("tts", result);
    }

    [Fact]
    public void VideoDir_EndsWithVideo()
    {
        var result = OutputPaths.VideoDir("活动");
        Assert.EndsWith("video", result);
    }
}
