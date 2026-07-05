using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ResItem = System.Collections.Generic.KeyValuePair<string, string>;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Parsing;

public partial class PrtsPreloader
{
    private List<string> ProcessImageCommand(StringDict commandDict)
    {
        GetImagesToOverride(commandDict);

        var isBg = commandDict.ContainsKey("type")
            && commandDict["type"].Equals("background", StringComparison.OrdinalIgnoreCase);
        var prefix = isBg ? "bg_" : "";
        var key = commandDict.TryGetValue("image", out var value)
            ? prefix + value.ToLower()
            : string.Empty;

        if (string.IsNullOrEmpty(key))
            return [];

        var url = _prts.GetImageUrl(key);
        if (string.IsNullOrEmpty(url))
        {
            Console.WriteLine($"<image> Linked key [{key}] not exist.");
            return [];
        }

        Assets.Add(new ResItem(key, url));
        return [url];
    }

    private List<string> ProcessPortraitCommand(StringDict commandDict)
    {
        GetCharactersToOverride(commandDict);

        var names = CollectCharacterNames(commandDict);
        var urls = new List<string>();

        foreach (var characterName in names)
        {
            var url = _prts.GetPortraitUrl(characterName);
            urls.Add(url);
            Assets.Add(new ResItem(characterName, url));
        }

        return urls;
    }

    private static List<string> CollectCharacterNames(StringDict commandDict)
    {
        var names = new List<string>();

        if (commandDict.TryGetValue("name", out var name))
            names.Add(name.ToLower());

        if (commandDict["type"] == "character" && commandDict.TryGetValue("name2", out var name2))
            names.Add(name2.ToLower());

        return names;
    }

    private List<string> ProcessLargeImageCommand(StringDict commandDict)
    {
        var urls = new List<string>();

        if (!commandDict.TryGetValue("imagegroup", out var imageGroupValue))
            return urls;

        var images = imageGroupValue.Split('/');
        foreach (var img in images)
        {
            var key = commandDict["type"].EndsWith("bg", StringComparison.OrdinalIgnoreCase)
                ? "bg_" + img.ToLower()
                : img.ToLower();

            if (!string.IsNullOrWhiteSpace(key))
            {
                var url = _prts.GetCharUrl(key);
                if (!string.IsNullOrEmpty(url))
                {
                    Assets.Add(new ResItem(key, url));
                }
                else
                {
                    Console.WriteLine($"<{commandDict["type"]}> Linked key [{key}] not exist.");
                }
            }
        }

        return urls;
    }

    private List<string> ProcessSoundsCommand(StringDict commandDict)
    {
        var audioKeys = CollectAudioKeys(commandDict);
        var urls = new List<string>();

        foreach (var audioKey in audioKeys)
        {
            var audioUrl = _prts.GetRealAudioUrl(audioKey);
            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                urls.Add(audioUrl);
                Assets.Add(new ResItem(audioKey, audioUrl));
            }
            else
            {
                Console.WriteLine($"<audio> Linked key [{audioKey}] not exist.");
            }
        }

        return urls;
    }

    private static List<string> CollectAudioKeys(StringDict commandDict)
    {
        var keys = new List<string>();

        if (commandDict["type"] == "playmusic" && commandDict.TryGetValue("intro", out var intro))
            keys.Add(intro);

        if (commandDict.TryGetValue("key", out var key))
            keys.Add(key);

        return keys;
    }

    internal static string SerializeCommandDict(StringDict commands)
    {
        var parts = new List<string>();
        foreach (var cmd in commands)
            parts.Add($"{cmd.Key}=\"{cmd.Value}\"");
        return $"[{commands["type"]}({string.Join(", ", parts)})]";
    }
}
