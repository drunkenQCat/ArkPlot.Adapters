namespace ArkPlot.Core.Services;

public class OpenWindowMessage
{
    public string WindowName { get; }
    public string? JsonPath { get; }
    public int? SelectedTabIndex { get; }
    public string? ActName { get; }
    public string? TurnKey { get; }

    /// <summary>发送方已解析好的故事输出目录（如复盘面板的 trace 根目录），接收方直接使用，不再自行拼接。</summary>
    public string? StoryOutputDir { get; }

    public OpenWindowMessage(
        string windowName,
        string? jsonPath = null,
        int? selectedTabIndex = null,
        string? currentActName = null,
        string? turnKey = null,
        string? storyOutputDir = null
    )
    {
        WindowName = windowName;
        JsonPath = jsonPath;
        SelectedTabIndex = selectedTabIndex;
        ActName = currentActName;
        TurnKey = turnKey;
        StoryOutputDir = storyOutputDir;
    }
}
