namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 图片描述服务接口。解耦视觉模型调用，支持不同游戏使用不同的视觉模型后端。
/// </summary>
public interface IImageDescriber
{
    /// <summary>根据图片 URL 生成散文描述。</summary>
    Task<string> DescribeAsync(string imageUrl);

    /// <summary>从散文描述中提取 YAML 结构化事实。</summary>
    Task<string> ExtractFactsAsync(string description);
}
