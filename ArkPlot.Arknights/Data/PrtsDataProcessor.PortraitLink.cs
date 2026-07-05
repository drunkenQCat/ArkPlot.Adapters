using System.Text.RegularExpressions;
using ArkPlot.Core.Services;

using ArkPlot.Arknights.Parsing;
namespace ArkPlot.Arknights.Data;

/// <summary>
///     Partial class for processing portrait data in the PrtsDataProcessor.
/// </summary>
public partial class PrtsDataProcessor
{
    private const string ThornsFallbackUrl = "https://media.prts.wiki/d/d0/Avg_char_293_thorns_1.png";

    /// <summary>
    ///     从原始角色名（如 "char_220_grani#3"）提取 CharacterCode（如 "char_220_grani"）。
    ///     内部复用 FindPortraitInLinkData 的解析逻辑。
    /// </summary>
    public string? GetCharacterCode(string rawName)
    {
        var (code, _) = FindPortraitInLinkData(rawName);
        return code != "-1" ? code : null;
    }

    /// <summary>
    ///     Retrieves the URL of a portrait based on the input key.
    /// </summary>
    /// <param name="inputKey">The key used to search for the portrait.</param>
    /// <returns>The URL of the portrait if found, otherwise a default URL.</returns>
    public string GetPortraitUrl(string inputKey)
    {
        (var key, var index) = FindPortraitInLinkData(inputKey);
        if (key == "-1")
        {
            new NotificationBlock().RaiseCommonEvent($"Character key [\"{key}\"] not exist, please check the link list");
            return GetOrLoadResource("char_293_thorns_1", "Char") ?? ThornsFallbackUrl;
        }

        var links = GetOrLoadLinks(key);
        if (links.Count == 0 || index < 0 || index >= links.Count)
        {
            new NotificationBlock().RaiseCommonEvent($"Character key [\"{key}\"] not exist, please check the link list");
            return GetOrLoadResource("char_293_thorns_1", "Char") ?? ThornsFallbackUrl;
        }

        var newKey = links[index].PortraitName;
        if (string.IsNullOrEmpty(newKey))
            new NotificationBlock().RaiseCommonEvent($"<character> Linked key [{key}] not exist.");

        newKey = string.IsNullOrEmpty(newKey) ? "char_293_thorns_1" : newKey.ToLower();
        return GetOrLoadResource(newKey, "Char") ?? ThornsFallbackUrl;
    }

    /// <summary>
    ///     Finds the portrait in the link data based on the provided key data.
    /// </summary>
    /// <param name="keyData">The key data used to find the portrait.</param>
    /// <returns>
    ///     A tuple containing the found portrait code and its index, or ("-1", -1) if the key data is empty or no key is
    ///     found.
    /// </returns>
    private (string, int) FindPortraitInLinkData(string keyData)
    {
        if (string.IsNullOrWhiteSpace(keyData))
        {
            Console.WriteLine("The input parameter is empty, has skipped the data.");
            return ("-1", -1);
        }

        var matchedCodeParts = ArkPlotRegs.CharPortraitCodeRegex().Match(keyData);
        if (!matchedCodeParts.Success)
        {
            Console.WriteLine("Can't get key from the input parameter, has skipped the data.");
            return ("-1", -1);
        }

        return ProcessMatchedCodeParts(matchedCodeParts);
    }

    private (string, int) ProcessMatchedCodeParts(Match matchedCodeParts)
    {
        var portraitNameGroup = matchedCodeParts.Groups[1].Value;
        var emotionIndex = GetSubIndex(3);

        var links = GetOrLoadLinks(portraitNameGroup);
        if (links.Count == 0)
        {
            Console.WriteLine($"The appointed key [{portraitNameGroup}] not exist, has skipped the data.");
            return ("-1", -1);
        }

        var groupIndex = GetSubIndex(4);
        var groupSubIndex = GetSubIndex(5);
        if (groupIndex is not null && groupSubIndex is not null) return ProcessDollarSymbol();

        if (!matchedCodeParts.Groups[2].Success) return (portraitNameGroup, Math.Max(emotionIndex ?? 1 - 1, 0));
        var symbol = matchedCodeParts.Groups[2].Value;

        switch (symbol)
        {
            case "@":
                return ProcessAtSymbol();
            case "$":
                return ProcessDollarSymbol();
            case "#":
                var outputIndex = ProcessHashSymbol();
                return (portraitNameGroup, outputIndex);
            default:
                return (portraitNameGroup,
                    Math.Max(emotionIndex ?? 1 - 1, 0));
        }

        (string portraitNameGroup, int) ProcessDollarSymbol()
        {
            var subIndex = "$" + (groupSubIndex ?? emotionIndex);
            emotionIndex = groupIndex ?? emotionIndex;

            var matchingElements = links
                .Select((l, idx) => new { Name = l.PortraitName, Index = idx })
                .Where(e => e.Name!.EndsWith(subIndex))
                .ToList();

            if (matchingElements.Count == 0)
            {
                Console.WriteLine($"No elements ending with {subIndex}.");
                return (portraitNameGroup, 0);
            }

            emotionIndex = Math.Min(emotionIndex ?? 0, matchingElements.Count - 1);
            var targetElement = matchingElements.ElementAt((int)emotionIndex);

            return (portraitNameGroup, targetElement.Index);
        }

        (string, int) ProcessAtSymbol()
        {
            for (var idx = 0; idx < links.Count; idx++)
            {
                if (links[idx].Alias == emotionIndex.ToString())
                    return (portraitNameGroup, idx);
            }

            Console.WriteLine("Data analyze error, use the default char to instead.");
            return (portraitNameGroup, 0);
        }

        int ProcessHashSymbol()
        {
            var outputIndex = emotionIndex ?? 0;
            if (outputIndex >= links.Count)
            {
                Console.WriteLine(
                    $"The analyze key [{portraitNameGroup} : {outputIndex}] is out of range, use the default char to instead");
                outputIndex = 0;
            }

            return outputIndex;
        }

        int? GetSubIndex(int index)
        {
            return matchedCodeParts.Groups[index].Success
                ? int.Parse(matchedCodeParts.Groups[index].Value)
                : null;
        }
    }
}
