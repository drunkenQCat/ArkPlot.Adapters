using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities;

namespace ArkPlot.Arknights.Data;

/// <summary>
/// PRTS 数据处理器：下载并解析 PRTS Wiki 的资源数据（图片/音频/覆盖规则/立绘链接）。
/// 职责拆分见同目录下的 partial 文件。
/// </summary>
public partial class PrtsDataProcessor
{
    public readonly PrtsAssets Res;
    private readonly NetworkUtility _http = new();

    public PrtsDataProcessor() : this(new PrtsAssets()) { }
    public PrtsDataProcessor(PrtsAssets assets) { Res = assets; }

    public async Task GetAllData()
    {
        var tasks = Res.AllData.Select(GetSingleData).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task GetSingleData(PrtsData singleData)
    {
        var prtsTemplateUrl = "https://prts.wiki/api.php?action=expandtemplates&format=json&text={{Widget:" +
            singleData.Tag + "}}";
        var query = await _http.GetAsync(prtsTemplateUrl);
        var csv = ProcessQuery(query);

        await Task.Run(() =>
        {
            if (csv is null)
            {
                new NotificationBlock().OnNetErrorHappen(
                    new NetworkErrorEventArgs($"{singleData.Tag} 无内容，请检查与prts的连接"));
                return;
            }

            if (singleData.Tag == "Data_Link")
            {
                Res.PortraitLinkDocument = GetPortraitLinkDocument(csv);
                return;
            }

            if (singleData.Tag == "Data_Override")
            {
                var overrideItems = LinesSplitter(csv);
                Res.DataOverrideDocument = ParseOverrideList(overrideItems);
                return;
            }

            var csvItems = LinesSplitter(csv);
            ParseItemList(singleData, csvItems);
        });
    }
}
