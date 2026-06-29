using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Adapters;

/// <summary>
/// 明日方舟资源解析器。
/// 包装 PrtsDataProcessor，将角色原始名映射为角色代码，并查询立绘/背景/音频 URL。
/// </summary>
public class ArknightsResourceResolver : IResourceResolver
{
    private readonly PrtsAssets _assets;
    private readonly PrtsDataProcessor _prts;

    public ArknightsResourceResolver() : this(new PrtsAssets()) { }

    public ArknightsResourceResolver(PrtsAssets assets)
    {
        _assets = assets;
        _prts = new PrtsDataProcessor(assets);
    }

    public string? NormalizeCharacterCode(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        return _prts.GetCharacterCode(rawName);
    }

    public string ResolvePortraitUrl(string characterCode, string? variant = null)
    {
        var key = string.IsNullOrEmpty(variant)
            ? characterCode
            : $"{characterCode}#{variant}";

        return _prts.GetPortraitUrl(key);
    }

    public string ResolveBackgroundUrl(string bgKey)
    {
        if (string.IsNullOrWhiteSpace(bgKey))
            return "https://media.prts.wiki/8/8a/Avg_bg_bg_black.png";

        if (_assets.DataImage.TryGetValue(bgKey.ToLower(), out var url))
            return url;

        return $"https://media.prts.wiki/{bgKey}";
    }

    public string ResolveAudioUrl(string audioKey)
    {
        if (string.IsNullOrWhiteSpace(audioKey))
            return "";

        if (_assets.DataAudio.TryGetValue(audioKey.ToLower(), out var url))
            return url;

        return $"{PrtsAssets.AudioAssetsUrl}{audioKey}";
    }
}
