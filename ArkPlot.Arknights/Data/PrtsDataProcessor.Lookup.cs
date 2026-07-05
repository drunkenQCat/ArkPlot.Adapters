using ArkPlot.Core.Infrastructure;
using SqlSugar;

namespace ArkPlot.Arknights.Data;

/// <summary>
/// ORM 懒加载 + 实例级缓存查找逻辑。
/// 替代旧的 PrtsAssets.Instance 全局单例 + JSON Document 查找。
/// </summary>
public partial class PrtsDataProcessor
{
    private readonly Dictionary<string, List<PrtsPortraitLink>> _linkCache = new();
    private readonly Dictionary<string, string?> _resourceCache = new();

    /// <summary>
    /// 查询某角色的所有立绘链接，首次查 DB 后走实例缓存。
    /// </summary>
    private List<PrtsPortraitLink> GetOrLoadLinks(string charCode)
    {
        if (_linkCache.TryGetValue(charCode, out var cached))
            return cached;

        var db = DbFactory.GetClient();
        var links = db.Queryable<PrtsPortraitLink>()
            .Where(l => l.CharacterCode == charCode)
            .OrderBy(l => l.SortOrder)
            .ToList();
        _linkCache[charCode] = links;
        return links;
    }

    /// <summary>
    /// 查询资源 URL（Char/Image/Audio），首次查 DB 后走实例缓存。
    /// </summary>
    private string? GetOrLoadResource(string resourceKey, string resourceType = "Char")
    {
        var cacheKey = $"{resourceType}:{resourceKey}";
        if (_resourceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var db = DbFactory.GetClient();
        var resource = db.Queryable<PrtsResource>()
            .Where(r => r.ResourceType == resourceType && r.ResourceKey == resourceKey)
            .First();
        var url = resource?.ResourceUrl;
        _resourceCache[cacheKey] = url;
        return url;
    }

    /// <summary>查角色立绘 URL（公开方法，供 PrtsPreloader 调用）。</summary>
    public string? GetCharUrl(string key) => GetOrLoadResource(key, "Char");

    /// <summary>查背景/图片 URL（公开方法，供 PrtsPreloader 调用）。</summary>
    public string? GetImageUrl(string key) => GetOrLoadResource(key, "Image");

    /// <summary>查音频 URL（公开方法，供 PrtsPreloader 调用）。</summary>
    public string? GetAudioUrl(string key) => GetOrLoadResource(key, "Audio");
}
