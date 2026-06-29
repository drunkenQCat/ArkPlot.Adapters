using ArkPlot.Core.Interfaces;
using ArkPlot.Core.Model;

namespace ArkPlot.FakeGame;

/// <summary>
/// 假假游戏标签渲染器：模板拼接式渲染，不需要 tag.json。
/// 用于验证 ITagRenderer 接口对不需要规则文件的游戏是否通用。
/// </summary>
public class FakeGameTagRenderer : ITagRenderer
{
    public bool RequiresRulesFile => false;

    public string DialogTemplate { get; set; } = "**{speaker}** {text}";

    public void LoadRules(string rulesFilePath)
    {
        // 不需要规则文件，空实现
    }

    public string RenderLine(ScriptLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Dialog))
            return line.OriginalText;

        // 通用类型名，不依赖方舟特有的类型
        if (line.Type == "dialog" && !string.IsNullOrEmpty(line.CharacterName))
        {
            var template = DialogTemplate;
            template = template.Replace("{speaker}", line.CharacterName);
            template = template.Replace("{text}", line.Dialog);
            return template;
        }

        return line.Dialog;
    }
}
