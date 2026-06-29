using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;
using ArkPlot.Core.Utilities;

namespace ArkPlot.FakeGame;

/// <summary>
/// 假假游戏剧情数据提供者：从 GitHub 下载 JSON 格式的剧情数据。
/// </summary>
public class FakeGameStoryProvider : IStoryDataProvider
{
    private readonly INetworkClient _http;

    public IReadOnlyList<string> SupportedLanguages => ["en", "zh"];

    public FakeGameStoryProvider(INetworkClient? http = null)
    {
        _http = http ?? new NetworkUtility();
    }

    public Task SyncMetadataAsync(string lang) => Task.CompletedTask;

    public async Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct = default)
    {
        var url = $"https://raw.githubusercontent.com/fakegame/data/main/{chapter.StoryCode}.json";
        return await _http.GetAsync(url, ct);
    }

    public async Task<string?> GetLatestVersionAsync(string lang)
    {
        var apiUrl = $"https://api.github.com/repos/fakegame/data/commits?per_page=1";
        return await _http.GetAsync(apiUrl);
    }

    public Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang)
    {
        var result = new Dictionary<string, (string Url, long ChapterId)>();
        foreach (var ch in chapters)
        {
            result[ch.StoryName] = (
                $"https://raw.githubusercontent.com/fakegame/data/main/{ch.StoryCode}.json",
                ch.Id
            );
        }
        return result;
    }
}
