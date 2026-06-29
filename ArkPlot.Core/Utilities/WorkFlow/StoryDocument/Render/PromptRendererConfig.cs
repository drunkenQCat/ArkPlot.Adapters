namespace ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

/// <summary>
/// PromptRenderer 的可配置项：游戏特有的标签类型名和 CSS class 名。
/// </summary>
public class PromptRendererConfig
{
    /// <summary>应跳过的音乐类标签类型。</summary>
    public HashSet<string> MusicSkipTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "playmusic", "stopmusic", "musicvolume", "musicstop", "soundvolume"
    };

    /// <summary>背景类标签类型（触发场景描述输出）。</summary>
    public HashSet<string> BackgroundTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "background", "largebg"
    };

    /// <summary>物品/CG 类标签类型（触发物品描述输出）。</summary>
    public HashSet<string> ItemTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "showitem", "cgitem", "interlude", "image"
    };

    /// <summary>角色描述 CSS class 名。</summary>
    public string PortraitFactsClass { get; set; } = "portrait-facts";

    /// <summary>场景描述 CSS class 名。</summary>
    public string SceneFactsClass { get; set; } = "scene-facts";

    /// <summary>物品描述 CSS class 名。</summary>
    public string ItemFactsClass { get; set; } = "item-facts";

    /// <summary>明日方舟默认配置。</summary>
    public static PromptRendererConfig Arknights => new();

    /// <summary>通用默认配置（空集合，不跳过任何类型）。</summary>
    public static PromptRendererConfig Generic => new()
    {
        MusicSkipTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        BackgroundTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "background" },
        ItemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image" },
    };
}
