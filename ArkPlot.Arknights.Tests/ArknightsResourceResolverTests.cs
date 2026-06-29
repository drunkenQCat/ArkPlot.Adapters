using ArkPlot.Core.Interfaces;
using ArkPlot.Arknights;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class ArknightsResourceResolverTests
{
    private readonly ArknightsResourceResolver _resolver = new();

    [Fact]
    public void NormalizeCharacterCode_EmptyInput_ReturnsNull()
    {
        Assert.Null(_resolver.NormalizeCharacterCode(""));
    }

    [Fact]
    public void NormalizeCharacterCode_NullInput_ReturnsNull()
    {
        Assert.Null(_resolver.NormalizeCharacterCode(null!));
    }

    [Fact]
    public void ResolveBackgroundUrl_EmptyKey_ReturnsDefaultBlackBg()
    {
        var url = _resolver.ResolveBackgroundUrl("");
        Assert.Contains("bg_black", url);
    }

    [Fact]
    public void ResolveAudioUrl_EmptyKey_ReturnsEmpty()
    {
        var url = _resolver.ResolveAudioUrl("");
        Assert.Equal("", url);
    }

    [Fact]
    public void ResolveAudioUrl_ValidKey_ReturnsUrlWithBaseUrl()
    {
        var url = _resolver.ResolveAudioUrl("bgm_test");
        Assert.StartsWith("https://torappu.prts.wiki/assets/", url);
    }

    [Fact]
    public void ResolveBackgroundUrl_UnknownKey_ReturnsFallbackUrl()
    {
        var url = _resolver.ResolveBackgroundUrl("unknown_bg_key");
        Assert.Contains("media.prts.wiki", url);
    }
}
