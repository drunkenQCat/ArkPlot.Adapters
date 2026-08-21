using System.Threading;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;

namespace ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

/// <summary>
/// 图片描述富化器：为 Entry 列表中的图片链接预填充 PicDesc 和 PicFacts。
/// 结果直接写入 entry.PicDesc / entry.PicFacts，后续管线无需再访问 PicDescService。
/// </summary>
public static class PicDescEnricher
{
    public static async Task EnrichAsync(IList<ScriptLine> entries, PicDescService picDescService, CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.ResourceUrls.Count == 0)
                continue;

            var isPortraitType = entry.Type is "character" or "charactercutin" or "charslot";

            List<string> urlsToDescribe;
            if (isPortraitType && entry.ResourceUrls.Count > 0)
            {
                // PortraitFocus and Portraits are Arknights-specific, removed for Core
                var resourceIndex = 0;
                if (resourceIndex >= entry.ResourceUrls.Count)
                    resourceIndex = 0;

                var focusUrl = entry.ResourceUrls[resourceIndex];
                urlsToDescribe = new List<string> { focusUrl };
                foreach (var url in entry.ResourceUrls)
                {
                    if (url != focusUrl)
                        urlsToDescribe.Add(url);
                }
            }
            else
            {
                urlsToDescribe = entry.ResourceUrls;
            }

            var characterCode = !string.IsNullOrEmpty(entry.CharacterCode)
                ? entry.CharacterCode : null;

            var descs = new List<string>();
            string? firstFacts = null;
            foreach (var url in urlsToDescribe)
            {
                var result = await picDescService.GetOrCreatePicDescWithFactsAsync(url, characterCode);
                if (!string.IsNullOrEmpty(result.Description))
                    descs.Add(result.Description);
                // 只保留第一个可用事实源（主立绘/主背景）：多段 YAML 用换行拼进
                // 同一个 entry.PicFacts 会落到同一个 <aside> 里，导致字段（colors 等）重复，
                // 或空值角色模板段混入场景描述。散文 PicDesc 仍保留全部，不丢信息。
                if (firstFacts is null && !string.IsNullOrEmpty(result.Facts))
                    firstFacts = result.Facts;
            }
            entry.PicDesc = string.Join("; ", descs);
            entry.PicFacts = firstFacts ?? "";
        }
    }
}