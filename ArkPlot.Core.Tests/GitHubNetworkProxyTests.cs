using System.Net;
using System.Net.Sockets;
using System.Text;
using ArkPlot.Core.Utilities;
using Xunit;

namespace ArkPlot.Core.Tests;

[CollectionDefinition("NetworkProxy", DisableParallelization = true)]
public class NetworkProxyCollection;

[Collection("NetworkProxy")]
public class GitHubNetworkProxyTests : IDisposable
{
    public void Dispose()
    {
        GitHubHttpClientFactory.Configure(GitHubNetworkMode.System, "");
        GitHubProxy.Prefix = "";
    }

    [Theory]
    [InlineData("http://127.0.0.1:7890")]
    [InlineData("https://127.0.0.1:7890")]
    [InlineData("socks5://127.0.0.1:1080")]
    public void CustomProxy_BypassesMirrorAndConfiguresHandler(string address)
    {
        GitHubProxy.Prefix = "https://broken-mirror.example/";
        GitHubHttpClientFactory.Configure(GitHubNetworkMode.Custom, address);
        using var handler = GitHubHttpClientFactory.CreateHandler();
        var url = new Uri("https://raw.githubusercontent.com/example/story.txt");
        Assert.Equal(url.AbsoluteUri, GitHubProxy.GetUrl(url.AbsoluteUri));
        Assert.True(handler.UseProxy);
        Assert.Equal(new Uri(address), handler.Proxy!.GetProxy(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("127.0.0.1:7890")]
    [InlineData("ftp://127.0.0.1:7890")]
    [InlineData("http://127.0.0.1:7890/path")]
    [InlineData("http://name:password@127.0.0.1:7890")]
    public void InvalidAddress_DoesNotReplaceActiveConfiguration(string address)
    {
        GitHubHttpClientFactory.Configure(GitHubNetworkMode.Direct, "");
        Assert.Throws<ArgumentException>(() => GitHubHttpClientFactory.Configure(GitHubNetworkMode.Custom, address));
        using var handler = GitHubHttpClientFactory.CreateHandler();
        Assert.False(handler.UseProxy);
    }

    [Fact]
    public void SystemMode_PreservesDefaultProxyBehavior()
    {
        GitHubHttpClientFactory.Configure(GitHubNetworkMode.System, "");
        using var handler = GitHubHttpClientFactory.CreateHandler();
        Assert.True(handler.UseProxy);
        Assert.Null(handler.Proxy);
    }

    [Fact]
    public async Task NetworkUtility_ActuallySendsRequestThroughConfiguredProxy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        GitHubHttpClientFactory.Configure(GitHubNetworkMode.Custom, $"http://127.0.0.1:{endpoint.Port}");
        var request = new NetworkUtility().GetAsync("http://proxy-test.invalid/story.txt", timeout.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        var firstLine = await reader.ReadLineAsync(timeout.Token);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(timeout.Token))) { }
        await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 10\r\nConnection: close\r\n\r\nstory-body"), timeout.Token);
        Assert.Equal("story-body", await request);
        Assert.Equal("GET http://proxy-test.invalid/story.txt HTTP/1.1", firstLine);
    }
}
