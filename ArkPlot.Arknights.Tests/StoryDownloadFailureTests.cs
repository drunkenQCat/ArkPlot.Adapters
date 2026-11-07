using ArkPlot.Arknights.Workflow;
using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class StoryDownloadFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDownload_StopsExportEvenWhenOtherChaptersSucceed(bool partialSuccess)
    {
        var loader = new AkpStoryLoader(
            new Act { Name = "人们，我们", Lang = "zh_CN" },
            [new StoryChapter { StoryId = "first", StoryCode = "PA-1", StoryName = "first", StoryTxt = "first" },
             new StoryChapter { StoryId = "second", StoryCode = "PA-2", StoryName = "second", StoryTxt = "second" }],
            networkClient: new FailedNetwork(partialSuccess));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.GetAllChapters());

        Assert.Contains("下载失败", error.Message);
        Assert.Empty(loader.ContentTable);
    }

    private sealed class FailedNetwork(bool partialSuccess) : INetworkClient
    {
        public Task<string> GetAsync(string url, CancellationToken ct = default) =>
            Task.FromResult(partialSuccess && url.EndsWith("first.txt") ? "[name=博士]你好" : "");
        public Task<string> GetJsonContent(string url) => throw new NotSupportedException();
    }
}
