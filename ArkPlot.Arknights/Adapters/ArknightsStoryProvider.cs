using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Adapters;

/// <summary>
/// 明日方舟剧情数据提供者。
/// 包装 StorySyncService + GitHubProxy，从 GitHub Kengxxiao 仓库下载剧情数据。
/// </summary>
public class ArknightsStoryProvider : IStoryDataProvider
{
    private readonly NetworkUtility _http = new();

    public IReadOnlyList<string> SupportedLanguages => ["zh_CN", "en_US", "ja_JP", "ko_KR", "zh_TW"];

    public async Task SyncMetadataAsync(string lang)
    {
        var svc = new StorySyncService();
        await svc.DownloadAndSaveAsync(lang);
    }

    public async Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
    {
        var url = BuildChapterUrl(chapter);
        if (string.IsNullOrEmpty(url))
            return null;

        var rawUrl = ConvertToRawUrl(url);
        return await _http.GetAsync(rawUrl, ct);
    }

    public async Task<string?> GetLatestVersionAsync(string lang)
    {
        var repo = GitHubProxy.GetRepoName(lang);
        var apiUrl = GitHubProxy.GetCommitApiUrl(repo);
        return await _http.GetAsync(apiUrl);
    }

    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang)
    {
        var result = new Dictionary<string, (string Url, long ChapterId)>();
        var baseUrl = GitHubProxy.GetStoryBaseUrl(lang);

        foreach (var ch in chapters)
        {
            var url = $"{baseUrl}{ch.StoryCode}.txt";
            result[ch.StoryName] = (url, ch.Id);
        }

        return result;
    }

    private static string BuildChapterUrl(StoryChapter chapter)
    {
        return chapter.StoryCode ?? "";
    }

    private static string ConvertToRawUrl(string githubUrl)
    {
        if (string.IsNullOrEmpty(githubUrl))
            return githubUrl;

        return githubUrl
            .Replace("github.com", "raw.githubusercontent.com")
            .Replace("/blob/", "/");
    }
}
