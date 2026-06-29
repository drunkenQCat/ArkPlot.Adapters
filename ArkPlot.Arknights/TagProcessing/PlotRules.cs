using System.IO;
using ArkPlot.Core.Model;
using Newtonsoft.Json.Linq;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Workflow;
using ArkPlot.Arknights.Parsing;
namespace ArkPlot.Arknights.TagProcessing;

/// <summary>
/// 明日方舟章节文字替换规则。
/// </summary>
public class PlotRules
{
    public JObject TagList;
    public readonly List<SentenceMethod> RegexAndMethods = new();

    public PlotRules()
    {
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.NameRegex(), PlotRegsBasicHelper.ProcessDialog));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.SegmentRegex(), PlotRegsBasicHelper.MakeLine));
        RegexAndMethods.Add(new SentenceMethod(ArkPlotRegs.CommentRegex(), PlotRegsBasicHelper.MakeComment));
        TagList = JObject.Parse("{}");
    }

    public void GetRegsFromJson(string jsonPath)
    {
        TagList = JObject.Parse(File.ReadAllText(jsonPath));
    }
}
