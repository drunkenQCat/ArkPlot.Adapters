using System.Text;
using System.Threading;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

namespace ArkPlot.Arknights;

/// <summary>
/// 明日方舟剧情处理器：导出 Markdown/HTML/Typst 格式。
/// 职责拆分见同目录下的 partial 文件。
/// </summary>
public abstract partial class AkpProcessor
{
    public static async Task<string> ExportPlotsAsync(
        List<PlotManager> plotList,
        PicDescService? picDescService = null,
        bool enableDescriptions = true,
        OutputMode outputMode = OutputMode.Readable,
        CancellationToken ct = default)
    {
        var md = new StringBuilder();
        foreach (var chapter in plotList)
        {
            ct.ThrowIfCancellationRequested();
            var textList = chapter.CurrentPlot.TextVariants;

            if (picDescService != null)
            {
                await PicDescEnricher.EnrichAsync(textList, picDescService, ct);
                PropagateCharacterCodeAndFacts(textList);
            }

            var builder = new StoryDocumentBuilder(textList, enableDescriptions, outputMode);
            md.Append($"## {chapter.CurrentPlot.Title}\r\n\r\n");
            builder.AppendResultToBuilder(md);
        }

        return md.ToString();
    }

    /// <summary>
    /// 传播 CharacterCode：从 charslot/character 条目传播到后续同名对话条目，
    /// 并将 PicFacts 复制给对话条目。
    /// </summary>
    private static void PropagateCharacterCodeAndFacts(IList<FormattedTextEntry> entries)
    {
        var nameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var codeToFacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingCode = null;
        string? pendingFacts = null;

        foreach (var entry in entries)
        {
            if (entry.Type is "character" or "charactercutin" or "charslot"
                && !string.IsNullOrEmpty(entry.CharacterCode))
            {
                pendingCode = entry.CharacterCode;
                if (!string.IsNullOrEmpty(entry.PicFacts))
                    pendingFacts = entry.PicFacts;
            }
            else if (!string.IsNullOrEmpty(entry.CharacterName) && string.IsNullOrEmpty(entry.CharacterCode))
            {
                ApplyPendingCode(entry, nameToCode, codeToFacts, ref pendingCode, ref pendingFacts);
            }
        }
    }

    private static void ApplyPendingCode(
        FormattedTextEntry entry,
        Dictionary<string, string> nameToCode,
        Dictionary<string, string> codeToFacts,
        ref string? pendingCode,
        ref string? pendingFacts)
    {
        if (nameToCode.TryGetValue(entry.CharacterName, out var knownCode))
        {
            entry.CharacterCode = knownCode;
            if (string.IsNullOrEmpty(entry.PicFacts) && codeToFacts.TryGetValue(knownCode, out var knownFacts))
                entry.PicFacts = knownFacts;
        }
        else if (pendingCode != null)
        {
            nameToCode[entry.CharacterName] = pendingCode;
            entry.CharacterCode = pendingCode;
            if (string.IsNullOrEmpty(entry.PicFacts) && pendingFacts != null)
            {
                codeToFacts[pendingCode] = pendingFacts;
                entry.PicFacts = pendingFacts;
            }
            pendingCode = null;
            pendingFacts = null;
        }
    }
}
