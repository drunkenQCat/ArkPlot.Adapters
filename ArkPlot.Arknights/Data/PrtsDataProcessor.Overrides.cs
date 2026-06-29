using System.Text.Json;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights.Data;

public partial class PrtsDataProcessor
{
    private JsonDocument ParseOverrideList(IEnumerable<string> lines)
    {
        var publicDisabled = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                continue;

            var parts = line.Split(':', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                continue;

            var key = parts[0].Trim().ToLower();
            var value = parts[1].Trim();
            switch (key)
            {
                case "title":
                    ParseTitle(value);
                    break;
                case "char":
                case "image":
                case "tween":
                case "override":
                    ParseKeyValueStructure(key, value);
                    break;
                case "disable":
                    ParseDisable(value, ref publicDisabled);
                    break;
            }
        }

        var json = JsonSerializer.Serialize(Res.RideItems);
        var options = new JsonDocumentOptions { AllowTrailingCommas = true };
        return JsonDocument.Parse(json, options);
    }

    private void ParseTitle(string value)
    {
        var parts = value.Split('=', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            return;

        var p = parts[0];
        var n = parts[1];
        if (!Res.RideItems.ContainsKey("title")) Res.RideItems["title"] = new Dictionary<string, object>();
        Res.RideItems["title"][p] = n;
    }

    private void ParseKeyValueStructure(string key, string value)
    {
        var mainParts = value.Split(';', 2);
        if (mainParts.Length != 2) return;

        var locationParts = mainParts[0].Split(',');
        if (locationParts.Length != 2) return;

        var page = locationParts[0];
        var line = locationParts[1];
        var values = mainParts[1].Split(',');

        var obj = new Dictionary<string, string>();
        foreach (var pair in values)
        {
            var kv = pair.Split('=');
            if (kv.Length == 2)
                obj[kv[0]] = kv[1];
        }

        if (!Res.RideItems.ContainsKey(key)) Res.RideItems[key] = new Dictionary<string, object>();
        if (!Res.RideItems[key].ContainsKey(page)) Res.RideItems[key][page] = new Dictionary<string, object>();
        ((Dictionary<string, object>)Res.RideItems[key][page])[line] = obj;
    }

    private void ParseDisable(string value, ref bool publicDisabled)
    {
        if (publicDisabled) return;

        var parts = value.Split(';');
        if (parts is ["public", _])
        {
            publicDisabled = true;
            if (!Res.RideItems.ContainsKey("disable")) Res.RideItems["disable"] = new Dictionary<string, object>();
            Res.RideItems["disable"]["note"] = parts[1];
        }
        else
        {
            foreach (var part in parts)
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || string.IsNullOrWhiteSpace(kv[1])) continue;

                switch (kv[0])
                {
                    case "prefix":
                    case "title":
                        if (!Res.RideItems.ContainsKey("disable"))
                            Res.RideItems["disable"] = new Dictionary<string, object>();
                        if (!Res.RideItems["disable"].ContainsKey(kv[0]))
                            Res.RideItems["disable"][kv[0]] = new Dictionary<string, object>();
                        ((Dictionary<string, object>)Res.RideItems["disable"][kv[0]])[kv[1]] = "";
                        break;
                    case "note":
                        if (!Res.RideItems.ContainsKey("disable"))
                            Res.RideItems["disable"] = new Dictionary<string, object>();
                        if (!Res.RideItems["disable"].ContainsKey("note"))
                            Res.RideItems["disable"]["note"] = new Dictionary<string, string>();
                        ((Dictionary<string, string>)Res.RideItems["disable"]["note"])[parts[0]] = kv[1];
                        break;
                }
            }
        }
    }
}
