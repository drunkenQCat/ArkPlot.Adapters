using ArkPlot.Core.Interfaces;

namespace ArkPlot.FakeGame;

/// <summary>
/// 假假游戏资源解析器：将角色代码映射为资源 URL。
/// 实际实现应从 GitHub/MediaWiki 拉取的映射表中查表，此处用简单规则模拟。
/// </summary>
public class FakeGameResourceResolver : IResourceResolver
{
    public string? NormalizeCharacterCode(string rawName)
    {
        // 假假游戏的角色代码就是 rawName 本身
        return string.IsNullOrWhiteSpace(rawName) ? null : rawName;
    }

    public string ResolvePortraitUrl(string characterCode, string? variant = null)
    {
        return string.IsNullOrWhiteSpace(characterCode)
            ? ""
            : $"https://fakegame.example.com/portrait/{characterCode}{variant}.png";
    }

    public string ResolveBackgroundUrl(string bgKey)
    {
        return string.IsNullOrWhiteSpace(bgKey)
            ? ""
            : $"https://fakegame.example.com/bg/{bgKey}.png";
    }

    public string ResolveAudioUrl(string audioKey)
    {
        return string.IsNullOrWhiteSpace(audioKey)
            ? ""
            : $"https://fakegame.example.com/bgm/{audioKey}.ogg";
    }
}
