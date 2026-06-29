using System.Threading;
using ArkPlot.Core.Model;

namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 剧情数据提供者：负责从特定游戏的远程数据源下载剧情元数据和原始文本。
/// 每种游戏实现一个适配器（如 ArkNights 从 GitHub Kengxxiao 仓库下载）。
/// </summary>
public interface IStoryDataProvider
{
    /// <summary>该游戏支持的语言代码列表。</summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// 同步活动/章节元数据到本地数据库。
    /// 实现应 upsert Acts 和 StoryChapters 表，保持 ID 稳定。
    /// </summary>
    Task SyncMetadataAsync(string lang);

    /// <summary>
    /// 下载指定章节的原始脚本内容。
    /// </summary>
    /// <returns>原始文本（含游戏特有标签语法），下载失败返回 null。</returns>
    Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default);

    /// <summary>
    /// 获取数据源的最新版本号（commit SHA、API 版本等），用于增量同步判断。
    /// </summary>
    Task<string?> GetLatestVersionAsync(string lang);

    /// <summary>
    /// 构造章节标题 → (下载 URL, StoryChapterId) 的映射。
    /// 用于管线批量下载时的 URL 查找。
    /// </summary>
    Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(List<StoryChapter> chapters, string lang);
}
