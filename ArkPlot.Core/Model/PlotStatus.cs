namespace ArkPlot.Core.Model;

/// <summary>
/// 剧情章节的处理状态。
/// </summary>
public enum PlotStatus
{
    /// <summary>未处理。</summary>
    Unprocessed = 0,

    /// <summary>已下载但未解析。</summary>
    Downloaded = 1,

    /// <summary>已解析完成，可用。</summary>
    Parsed = 2,
}
