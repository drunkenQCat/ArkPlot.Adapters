using System.Text.Json;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights;

public partial class PrtsDataProcessor
{
    private static void ParseItemList(PrtsData prts, IEnumerable<string> itemList)
    {
        var csvDict = prts.Data;
        if (prts.Tag == "Data_Audio")
            ParseAudioCsv(itemList, csvDict);
        else
            ParseNormalCsv(itemList, csvDict);
        prts.Data = csvDict;
    }

    private static void ParseAudioCsv(IEnumerable<string> jsonItems, StringDict csvDict)
    {
        foreach (var item in jsonItems)
        {
            var keyValue = item.Trim();
            var audioJsonItem = ParseAudioJsonItem(keyValue);
            if (audioJsonItem == null) continue;
            csvDict[audioJsonItem[0]] = GetAudioLink(audioJsonItem[1]);
        }
    }

    private static void ParseNormalCsv(IEnumerable<string> csvItems, StringDict csvDict)
    {
        foreach (var item in csvItems)
        {
            var keyValue = item.Trim().Split(",");
            if (keyValue.Length != 2) continue;
            csvDict[keyValue[0]] = keyValue[1];
        }
    }

    private static string GetAudioLink(string url)
    {
        url = url.ToLower();
        var urlToken = url.Split('/');
        urlToken[0] = "audio";
        return PrtsAssets.AudioAssetsUrl + string.Join("/", urlToken) + ".mp3";
    }

    public string GetRealAudioUrl(string audioKey)
    {
        if (string.IsNullOrEmpty(audioKey)) return "";

        var audioKeyLower = audioKey.ToLower();

        if (audioKey.StartsWith('$'))
            return Res.DataAudio.TryGetValue(audioKeyLower[1..], out var audioUrl) ? audioUrl : "";

        if (audioKey.StartsWith('@'))
            return string.Concat(PrtsAssets.AudioAssetsUrl, audioKeyLower[1..]);

        return PrtsAssets.AudioAssetsUrl + audioKeyLower.Replace("sound_beta_2", "audio") + ".mp3";
    }

    private static string[]? ParseAudioJsonItem(string jsonItem)
    {
        if (!jsonItem.Contains("Sound")) return null;

        var items = jsonItem.Replace("\"", "").Replace(",", "").Split(':');
        items = (from i in items select i.Trim()).ToArray();
        return items;
    }

    private static string? ProcessQuery(string inputQuery)
    {
        var jsonElement = JsonDocument.Parse(inputQuery).RootElement
            .GetProperty("expandtemplates").GetProperty("*");
        return jsonElement.GetString();
    }

    private static IEnumerable<string> LinesSplitter(string plot)
    {
        return plot.Split("\n");
    }

    private static JsonDocument GetPortraitLinkDocument(string portraitLinkJson)
    {
        return JsonDocument.Parse(portraitLinkJson);
    }
}
