using ArkPlot.Core.Model;
using ArkPlot.Core.Services;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

public partial class PrtsPreloader
{
    private static (List<string> Portraits, int Focus) GetCurrentPortraitsFromCharacter(
        StringDict commandDict,
        List<string> inputUrls)
    {
        var urls = new List<string>(inputUrls);
        if (urls.Count == 0)
            return (["https://pics/transparent.png"], 0);

        if (commandDict.TryGetValue("focus", out var position))
        {
            var focus = position switch
            {
                "-1" => -1,
                "1" => 1,
                "2" => 2,
                _ => 0,
            };
            return (urls, focus);
        }

        return (urls, 0);
    }

    private (List<string> Portraits, int Focus) GetCurrentPortraitsFromSlot(
        StringDict commandDict,
        List<string> urls)
    {
        if (!commandDict.TryGetValue("slot", out var slot))
            return (urls, 0);

        var isFocusNone = commandDict.TryGetValue("focus", out var focus) && focus == "none";
        // 只有"显式移除立绘"的 charslot 才清理槽位：无 name、无 action 且非纯焦点切换。
        // 带 action 的指令（如 shake/power/times）只是对已有立绘做动画，不应清除该槽位的立绘；
        // 带 focus（非 none）的指令只是把镜头焦点切到某槽，该槽立绘应继续在场，
        // 否则后续对话会把 focus 误判到另一个槽位的立绘上。
        var isFocusSwitch = commandDict.TryGetValue("focus", out var focusVal) && focusVal != "none";
        var isRemoving = (!commandDict.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
            && !commandDict.ContainsKey("action")
            && !isFocusSwitch;

        if (isRemoving)
            _slotUrls.Remove(slot);
        else if (urls.Count > 0)
            _slotUrls[slot] = urls[0];

        var merged = MergeSlotUrls(isFocusNone ? null : slot, out var currentFocus);

        if (merged.Count == 0)
            return (["https://pics/transparent.png"], 0);

        return (merged, currentFocus);
    }

    private List<string> MergeSlotUrls(string? focusSlot, out int currentFocus)
    {
        var merged = new List<string>();
        currentFocus = 0;
        string[] slotOrder = ["m", "l", "r"];

        foreach (var s in slotOrder)
        {
            if (_slotUrls.TryGetValue(s, out var url))
            {
                if (s == focusSlot)
                    currentFocus = merged.Count;
                merged.Add(url);
            }
        }

        return merged;
    }
}
