using System.Net;
using System.Net.Http;

namespace ArkPlot.Core.Utilities;

public enum GitHubNetworkMode { System, Direct, Custom }

/// <summary>Transport proxy for GitHub downloads, separate from mirror URL prefixes.</summary>
public static class GitHubHttpClientFactory
{
    private sealed record Configuration(GitHubNetworkMode Mode, string Address);
    private static Configuration _configuration = new(GitHubNetworkMode.System, "");

    public static bool UsesCustomProxy => _configuration.Mode == GitHubNetworkMode.Custom;

    public static void Configure(GitHubNetworkMode mode, string? address)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentException("请选择有效的连接方式。");
        address = address?.Trim() ?? "";
        if (mode == GitHubNetworkMode.Custom &&
            (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
             uri.Scheme is not ("http" or "https" or "socks5") ||
             string.IsNullOrEmpty(uri.Host) || uri.Port <= 0 ||
             uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0))
            throw new ArgumentException("请输入有效的代理地址，例如 http://127.0.0.1:7890 或 socks5://127.0.0.1:1080（不含用户名、密码或路径）。");

        _configuration = new(mode, address);
    }

    public static HttpClient CreateClient() => new(CreateHandler());

    internal static HttpClientHandler CreateHandler()
    {
        var configuration = _configuration;
        return configuration.Mode switch
        {
            GitHubNetworkMode.Direct => new HttpClientHandler { UseProxy = false },
            GitHubNetworkMode.Custom => new HttpClientHandler
            {
                UseProxy = true,
                Proxy = new WebProxy(configuration.Address) { BypassProxyOnLocal = false }
            },
            _ => new HttpClientHandler()
        };
    }
}
