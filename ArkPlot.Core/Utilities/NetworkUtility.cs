using System.Net.Http;
using System.Threading;
using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Services;

namespace ArkPlot.Core.Utilities;

/// <summary>
/// HTTP 网络客户端。可通过 INetworkClient 注入，也可通过静态方法直接调用。
/// </summary>
public class NetworkUtility : INetworkClient
{
    private readonly NotificationBlock _notify;

    public NetworkUtility(NotificationBlock? notify = null)
    {
        _notify = notify ?? new NotificationBlock();
    }

    public async Task<string> GetAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                GitHubProxy.CheckConnectionError(url, statusCode: (int)response.StatusCode);
                if (response.ReasonPhrase != null)
                    _notify.OnNetErrorHappen(new NetworkErrorEventArgs(response));
                return "";
            }
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            GitHubProxy.CheckConnectionError(url, exception: e);
            _notify.OnNetErrorHappen(new NetworkErrorEventArgs(e.Message));
            return "";
        }
    }

    public async Task<string> GetJsonContent(string url)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage();
        request.RequestUri = new Uri(url);
        request.Method = HttpMethod.Get;
        request.Headers.Add("Accept", "application/json");
        try
        {
            var response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException e)
        {
            GitHubProxy.CheckConnectionError(url, exception: e);
            _notify.OnNetErrorHappen(new NetworkErrorEventArgs(e.Message));
            return "";
        }
    }
}
