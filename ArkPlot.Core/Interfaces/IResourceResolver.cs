namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 资源解析器：将游戏内资源标识符（角色代码、背景键、音频键）转换为可下载的 URL。
/// 每种游戏的资源命名和托管方式不同，需要各自实现。
/// </summary>
public interface IResourceResolver
{
    /// <summary>
    /// 将角色原始名（如 "阿米娅"）归一化为标准角色代码（如 "char_002_amiya"）。
    /// 无法识别时返回 null。
    /// </summary>
    string? NormalizeCharacterCode(string rawName);

    /// <summary>
    /// 根据角色代码获取立绘图片 URL。
    /// </summary>
    /// <param name="characterCode">标准角色代码</param>
    /// <param name="variant">可选的变体/表情后缀</param>
    string ResolvePortraitUrl(string characterCode, string? variant = null);

    /// <summary>
    /// 根据背景标识获取背景图 URL。
    /// </summary>
    string ResolveBackgroundUrl(string bgKey);

    /// <summary>
    /// 根据音频标识获取音频 URL。
    /// </summary>
    string ResolveAudioUrl(string audioKey);
}
