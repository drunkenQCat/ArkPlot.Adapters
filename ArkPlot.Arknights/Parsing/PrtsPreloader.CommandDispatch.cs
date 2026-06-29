using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;
using ResItem = System.Collections.Generic.KeyValuePair<string, string>;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

public partial class PrtsPreloader
{
    /// <summary>根据命令类型分发处理，返回解析后的参数表和关联的 URL 列表。</summary>
    private StringDict ProcessCommand(string command, string parameters, out List<string> urls)
    {
        var dict = parameters.ToCommandSet();
        dict["type"] = command;
        urls = [];

        switch (command)
        {
            case "background":
            case "image":
            case "showitem":
                urls = ProcessImageCommand(dict);
                UpdateBackgroundFromUrls(urls);
                break;

            case "backgroundtween":
            case "imagetween":
            case "largebgtween":
            case "largeimgtween":
                GetTweensToOverride(dict);
                urls = ProcessPortraitCommand(dict);
                UpdatePortraitsFromCharacter(dict, urls);
                break;

            case "character":
                urls = HandleCharacterCommand(dict);
                break;

            case "charactercutin":
                urls = ProcessPortraitCommand(dict);
                (_currentPortraits, _currentPortraitFocus) = (urls.Count > 0 ? urls : ["https://pics/transparent.png"], 0);
                break;

            case "charslot":
                urls = HandleCharslotCommand(dict);
                break;

            case "gridbg":
            case "verticalbg":
            case "largebg":
            case "largeimg":
                urls = ProcessLargeImageCommand(dict);
                UpdateBackgroundFromUrls(urls);
                break;

            case "playmusic":
            case "playsound":
                urls = ProcessSoundsCommand(dict);
                break;
        }

        return dict;
    }

    private void UpdateBackgroundFromUrls(List<string> urls)
    {
        _currentBg = (urls.Count != 0 && !string.IsNullOrEmpty(urls[0]))
            ? urls[0]
            : DefaultBg;
    }

    private List<string> HandleCharacterCommand(StringDict dict)
    {
        if (!dict.TryGetValue("name", out var cName) || string.IsNullOrEmpty(cName))
        {
            ClearPortraitState();
            return [];
        }

        var urls = ProcessPortraitCommand(dict);
        UpdatePortraitsFromCharacter(dict, urls);
        return urls;
    }

    private List<string> HandleCharslotCommand(StringDict dict)
    {
        if (!dict.TryGetValue("slot", out var slotVal) || string.IsNullOrEmpty(slotVal))
        {
            ClearPortraitState();
            return [];
        }

        var urls = ProcessPortraitCommand(dict);
        (_currentPortraits, _currentPortraitFocus) = GetCurrentPortraitsFromSlot(dict, urls);

        if (dict.TryGetValue("focus", out var csFocus) && csFocus == "none")
            urls.Clear();

        return urls;
    }

    private void ClearPortraitState()
    {
        _slotUrls.Clear();
        _currentPortraits = ["https://pics/transparent.png"];
        _currentPortraitFocus = 0;
    }

    private void UpdatePortraitsFromCharacter(StringDict dict, List<string> urls)
    {
        (_currentPortraits, _currentPortraitFocus) = GetCurrentPortraitsFromCharacter(dict, urls);
    }
}
