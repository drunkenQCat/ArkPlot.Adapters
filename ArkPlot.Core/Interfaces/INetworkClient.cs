using System.Threading;

namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 网络客户端接口，用于解耦静态 NetworkUtility，支持测试 mock。
/// </summary>
public interface INetworkClient
{
    Task<string> GetAsync(string url, CancellationToken ct = default);
    Task<string> GetJsonContent(string url);
}
