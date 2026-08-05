using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Workflow;
namespace ArkPlot.Arknights.TagProcessing;

public partial class TagProcessor
{
    private string GetImageUrl(string newTag, string newValueTrimed)
    {
        if (IsBackgroundTag(newTag)) newValueTrimed = $"bg_{newValueTrimed}";
        return _prts.GetImageUrl(newValueTrimed) ?? "";
    }

    private static bool IsBackgroundTag(string newTag)
    {
        return newTag.Contains('景') || newTag.Contains("拼图");
    }

    private string ConvertToImageTag(string newTag, string newValue)
    {
        if (newValue.Contains('/'))
        {
            var keys = newValue.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();
            var tags = keys.Select(k =>
                $"<img src=\"{GetImageUrl(newTag, k)}\" alt=\"{k}\" loading=\"lazy\" style=\"max-height:350px\"/>");
            return string.Join("\r\n\r\n", tags);
        }

        var url = GetImageUrl(newTag, newValue);
        return $"<img src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:350px\"/>";
    }

    private string ConvertToAudioTag(string newTag, string newValue)
    {
        var url = _prts.GetRealAudioUrl(newValue);
        url =
            $"<audio controls class=\"lazy-audio\" width=\"300\" alt=\"{newValue}\"><source src=\"{url}\" type=\"audio/mpeg\"></audio>";
        if (newTag.Contains('乐'))
        {
            var urlParts = url.Split(" ");
            urlParts[0] += " class=\"music\"";
            url = string.Join(" ", urlParts);
        }

        return url;
    }

    private string ConvertToPortraitTag(string newValue)
    {
        var url = _prts.GetCharUrl(newValue) ?? "";
        return
            $"<img class=\"portrait\" src=\"{url}\" alt=\"{newValue}\" loading=\"lazy\" style=\"max-height:300px\"/>";
    }
}
