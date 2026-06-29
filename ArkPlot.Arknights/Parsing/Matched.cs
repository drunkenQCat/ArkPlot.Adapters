using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

/// <summary>正则匹配结果，包含标签、命令、纯标签和角色名。</summary>
internal class Matched
{
    public string Tag { get; set; } = "";
    public string Commands { get; set; } = "";
    public string TagOnly { get; set; } = "";
    public string CharName { get; set; } = "";
}
