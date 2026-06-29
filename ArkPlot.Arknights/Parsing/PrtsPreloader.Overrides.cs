using System.Text.Json;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

public partial class PrtsPreloader
{
    private void OverrideCurrentText()
    {
        if (!_isTextNeedsOverride) return;

        if (!TryGetPageOverride("override", out var pageOverrides)) return;
        if (!pageOverrides.TryGetProperty((_counter + 1).ToString(), out var lineOverrides)) return;

        if (lineOverrides.ValueKind == JsonValueKind.Object)
            _textList[_counter].OriginalText = lineOverrides.EnumerateObject().First().ToString();
    }

    private void GetCharactersToOverride(StringDict commandDict)
    {
        if (!_isCharacterNeedsOverride) return;

        if (!TryGetPageOverride("char", out var pageOverrides)) return;
        if (!pageOverrides.TryGetProperty((_counter + 1).ToString(), out var overrides)) return;
        if (overrides.ValueKind != JsonValueKind.Object) return;

        OverrideCharacterNames(commandDict, overrides);
        _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
    }

    private static void OverrideCharacterNames(StringDict commandDict, JsonElement overrides)
    {
        var originalName = commandDict["name"];
        if (overrides.TryGetProperty("name", out var name1))
            commandDict["name"] = name1.ToString();
        else
            commandDict["name"] = originalName;

        if (commandDict["type"] != "character") return;

        var originalName2 = commandDict["name2"];
        if (overrides.TryGetProperty("name2", out var name2))
            commandDict["name2"] = name2.ToString();
        else
            commandDict["name2"] = originalName2;
    }

    private void GetImagesToOverride(StringDict commandDict)
    {
        if (!_isImageNeedsOverride) return;

        if (!TryGetPageOverride("image", out var pageOverrides)) return;
        if (!pageOverrides.TryGetProperty((_counter + 1).ToString(), out var overrides)) return;
        if (overrides.ValueKind != JsonValueKind.Object) return;

        ApplyPropertyOverrides(commandDict, overrides);
        _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
    }

    private void GetTweensToOverride(StringDict commandDict)
    {
        if (!_isTweenNeedsOverride) return;

        if (!TryGetPageOverride("tween", out var pageOverrides)) return;
        if (!pageOverrides.TryGetProperty((_counter + 1).ToString(), out var overrides)) return;
        if (overrides.ValueKind != JsonValueKind.Object) return;

        ApplyPropertyOverrides(commandDict, overrides);
        _textList[_counter].OriginalText = SerializeCommandDict(commandDict);
    }

    private static void ApplyPropertyOverrides(StringDict commandDict, JsonElement overrides)
    {
        foreach (var property in overrides.EnumerateObject())
        {
            var originalValue = commandDict[property.Name];
            commandDict[property.Name] = property.Value.GetString() ?? originalValue;
        }
    }

    /// <summary>
    /// 从 DataOverrideDocument 中获取当前页面的覆盖数据。
    /// 失败时自动将对应的 override 标志置为 false，避免重复查找。
    /// </summary>
    private bool TryGetPageOverride(string category, out JsonElement pageOverrides)
    {
        pageOverrides = default;

        var root = _prts.Res.DataOverrideDocument.RootElement;
        if (!root.TryGetProperty(category, out var categoryData))
        {
            SetOverrideFlagFalse(category);
            return false;
        }

        if (!categoryData.TryGetProperty(_pageName, out pageOverrides))
        {
            SetOverrideFlagFalse(category);
            return false;
        }

        return true;
    }

    private void SetOverrideFlagFalse(string category)
    {
        switch (category)
        {
            case "override": _isTextNeedsOverride = false; break;
            case "char": _isCharacterNeedsOverride = false; break;
            case "image": _isImageNeedsOverride = false; break;
            case "tween": _isTweenNeedsOverride = false; break;
        }
    }
}
