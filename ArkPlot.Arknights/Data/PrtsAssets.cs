using System.Text.Json;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights.Data;

/// <summary>
/// PRTS 资源数据容器：管理音频、角色图片、背景图片等资源数据字典。
/// </summary>
public class PrtsAssets
{
    public static PrtsAssets Instance { get; } = new();

    public const string AudioAssetsUrl = "https://torappu.prts.wiki/assets/";

    public List<PrtsData> AllData;
    public StringDict DataAudio = new();
    public StringDict DataChar = new();
    public StringDict DataImage = new();
    public Dictionary<string, Dictionary<string, object>> RideItems = new();
    public JsonDocument DataOverrideDocument = JsonDocument.Parse("{ }");
    public JsonDocument PortraitLinkDocument = JsonDocument.Parse("{ }");
    public StringDict PreLoaded = new();

    public PrtsAssets()
    {
        AllData = new List<PrtsData>
        {
            new("Data_Image", DataImage),
            new("Data_Char", DataChar),
            new("Data_Audio", DataAudio),
            new("Data_Link"),
            new("Data_Override")
        };
    }

    public void RestoreAllData()
    {
        DataImage = AllData[0].Data;
        DataChar = AllData[1].Data;
        DataAudio = AllData[2].Data;
    }
}
