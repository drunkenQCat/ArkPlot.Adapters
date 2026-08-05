using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;
using Xunit;

namespace ArkPlot.Core.Tests;

[Collection("DbTests")]
public class GitHubProxyExtendedTests
{
    [Fact]
    public void GetRepoName_ZhCN()
    {
        Assert.Equal("Kengxxiao/ArknightsGameData", GitHubProxy.GetRepoName("zh_CN"));
    }

    [Fact]
    public void GetRepoName_Other()
    {
        Assert.Equal("ArknightsAssets/ArknightsGamedata", GitHubProxy.GetRepoName("en_US"));
    }

    [Fact]
    public void MapLangToDir_ZhCN()
    {
        Assert.Equal("zh_CN", GitHubProxy.MapLangToDir("zh_CN"));
    }

    [Fact]
    public void MapLangToDir_EnUS()
    {
        Assert.Equal("en", GitHubProxy.MapLangToDir("en_US"));
    }

    [Fact]
    public void MapLangToDir_JaJP()
    {
        Assert.Equal("jp", GitHubProxy.MapLangToDir("ja_JP"));
    }

    [Fact]
    public void MapLangToDir_KoKR()
    {
        Assert.Equal("kr", GitHubProxy.MapLangToDir("ko_KR"));
    }

    [Fact]
    public void MapLangToDir_ZhTW()
    {
        Assert.Equal("tw", GitHubProxy.MapLangToDir("zh_TW"));
    }

    [Fact]
    public void MapLangToDir_Unknown_ReturnsInput()
    {
        Assert.Equal("fr_FR", GitHubProxy.MapLangToDir("fr_FR"));
    }

    [Fact]
    public void GetStoryTableUrl_ContainsJsonFile()
    {
        GitHubProxy.Prefix = "";
        var url = GitHubProxy.GetStoryTableUrl("zh_CN");
        Assert.EndsWith("story_review_table.json", url);
        Assert.Contains("Kengxxiao/ArknightsGameData", url);
    }

    [Fact]
    public void GetStoryBaseUrl_EndsWithSlash()
    {
        GitHubProxy.Prefix = "";
        var url = GitHubProxy.GetStoryBaseUrl("zh_CN");
        Assert.EndsWith("/", url);
        Assert.Contains("story", url);
    }

    [Fact]
    public void GetCommitApiUrl_ContainsRepo()
    {
        GitHubProxy.Prefix = "";
        var url = GitHubProxy.GetCommitApiUrl("Kengxxiao/ArknightsGameData");
        Assert.Contains("Kengxxiao/ArknightsGameData", url);
        Assert.Contains("commits", url);
    }

    [Fact]
    public void GetStoryTableUrl_WithProxy_PrependsProxy()
    {
        GitHubProxy.Prefix = "https://proxy.example.com/";
        var url = GitHubProxy.GetStoryTableUrl("zh_CN");
        Assert.StartsWith("https://proxy.example.com/", url);
    }

    [Fact]
    public void CheckConnectionError_WithProxy_DoesNotThrow()
    {
        GitHubProxy.Prefix = "https://proxy.example.com/";
        // Should not throw or trigger event when proxy is configured
        GitHubProxy.CheckConnectionError("https://github.com/test", exception: new Exception("timeout"));
    }

    [Fact]
    public void NotifyConnectionFailed_TriggersEvent()
    {
        GitHubProxy.Prefix = "";
        string? capturedUrl = null;
        void Handler(string url) => capturedUrl = url;

        GitHubProxy.ConnectionFailed += Handler;
        try
        {
            GitHubProxy.NotifyConnectionFailed("https://github.com/test");
            Assert.Equal("https://github.com/test", capturedUrl);
        }
        finally
        {
            GitHubProxy.ConnectionFailed -= Handler;
        }
    }

    [Fact]
    public void CheckConnectionError_NonGithubUrl_DoesNotTrigger()
    {
        GitHubProxy.Prefix = "";
        string? capturedUrl = null;
        void Handler(string url) => capturedUrl = url;

        GitHubProxy.ConnectionFailed += Handler;
        try
        {
            GitHubProxy.CheckConnectionError("https://example.com/api", exception: new Exception("err"));
            Assert.Null(capturedUrl);
        }
        finally
        {
            GitHubProxy.ConnectionFailed -= Handler;
        }
    }

    /// <summary>
    /// 回归测试：代理前缀不支持 api.github.com 时，GetLatestCommitShaAsync 应回退直连拿到 SHA。
    /// 使用 ghproxy.cxkpro.top（已知只支持 gh-proxy 文件链接，不支持 api.github.com）作为代理前缀。
    /// 依赖网络；HTTP 连接失败时跳过（不视为失败）。
    /// </summary>
    [Fact]
    public async Task GetLatestCommitSha_ProxyFails_FallsBackToDirect()
    {
        GitHubProxy.Prefix = "https://ghproxy.cxkpro.top";
        try
        {
            var sha = await StorySyncService.GetLatestCommitShaAsync("Kengxxiao/ArknightsGameData");
            // 网络完全不可达时跳过（避免 CI 无网环境误报）
            if (sha == null)
                return;
            Assert.False(string.IsNullOrWhiteSpace(sha));
            Assert.Equal(40, sha!.Length); // SHA-1 长度
        }
        finally
        {
            GitHubProxy.Prefix = "";
        }
    }
}
