using System.Text;
using System.IO;
using System.Threading;
using ArkPlot.Core.Model;
using ArkPlot.Core.Services;
using ArkPlot.Core.Utilities.WorkFlow.StoryDocument;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Workflow;

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
        CancellationToken ct = default,
        Action<string>? onLog = null)
    {
        var md = new StringBuilder();
        foreach (var chapter in plotList)
        {
            ct.ThrowIfCancellationRequested();
            onLog?.Invoke($"导出文档：{chapter.CurrentPlot.Title}");
            var textList = chapter.CurrentPlot.TextVariants;

            if (picDescService != null)
            {
                await PicDescEnricher.EnrichAsync(textList, picDescService, ct);
                PropagateCharacterCodeAndFacts(textList);
            }

            NormalizeDisplayNames(textList);

            var builder = new StoryDocumentBuilder(textList, enableDescriptions, outputMode);
            md.Append($"## {chapter.CurrentPlot.Title}\r\n\r\n");
            builder.AppendResultToBuilder(md);
        }

        return md.ToString();
    }

    /// <summary>
    /// 统一规范化角色展示名。把「？？？」这类占位名统一为「神秘人士」，
    /// 确保 aside 的 data-character 与正文对话名（ProcessDialog 的转换）一致，
    /// 避免小说化时 LLM 无法对应立绘描述与说话角色。
    ///
    /// 若该占位角色在剧情中揭示了真名（如「？？？」→「洛伦茨」），
    /// 则 aside 的 data-character 标注为「神秘人士（实际人物：洛伦茨）」，
    /// 给 LLM 明确暗示，使其知道占位名与真名是同一人。
    /// </summary>
    private static void NormalizeDisplayNames(IList<ScriptLine> entries)
    {
        // 先扫描：CharacterCode → 揭示名（非「？？？」的角色名）。
        // 同一 leave code 下，若存在已揭示的真名（如「洛伦茨」），则之后可用于标注占位名。
        var codeToRevealedName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OfType<FormattedTextEntry>())
        {
            if (string.IsNullOrEmpty(entry.CharacterName)
                || entry.CharacterName == "？？？"
                || string.IsNullOrEmpty(entry.CharacterCode))
                continue;

            if (!codeToRevealedName.ContainsKey(entry.CharacterCode))
                codeToRevealedName[entry.CharacterCode] = entry.CharacterName;
        }

        foreach (var entry in entries.OfType<FormattedTextEntry>())
        {
            if (entry.CharacterName != "？？？")
                continue;

            if (!string.IsNullOrEmpty(entry.CharacterCode)
                && codeToRevealedName.TryGetValue(entry.CharacterCode, out var revealed))
            {
                entry.CharacterName = $"神秘人士（实际人物：{revealed}）";
            }
            else
            {
                entry.CharacterName = "神秘人士";
            }
        }
    }
    private static void PropagateCharacterCodeAndFacts(IList<ScriptLine> entries)
    {
        var nameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var codeToFacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pendingCode = null;
        string? pendingFacts = null;
        // 当前是否有"在场且聚焦"的立绘。focus="none" 表示画外音（立绘退场/隐藏），
        // 此时 aphris 等立绘虽仍在 Portraits 里，但说话者不是它，不能把它的 code 传播给对话。
        var hasLiveFocus = true;

        foreach (var entry in entries.OfType<FormattedTextEntry>())
        {
            if (entry.Type is "character" or "charactercutin" or "charslot"
                && !string.IsNullOrEmpty(entry.CharacterCode))
            {
                // 立绘行：更新焦点状态。focus="none" 的画外音立绘不提供说话者归属。
                hasLiveFocus = !IsFocusNone(entry);
                if (hasLiveFocus)
                {
                    pendingCode = entry.CharacterCode;
                    if (!string.IsNullOrEmpty(entry.PicFacts))
                        pendingFacts = entry.PicFacts;
                }
                else
                {
                    // 画外音：立绘退场，清空 pending，避免 aphris 的 code 污染后续对话。
                    pendingCode = null;
                    pendingFacts = null;
                }
            }
            else if (!string.IsNullOrEmpty(entry.CharacterName))
            {
                ApplyPendingCode(entry, nameToCode, codeToFacts, ref pendingCode, ref pendingFacts, hasLiveFocus);
            }
        }
    }

    /// <summary>判断立绘行的 focus 是否为 "none"（画外音/退场）。</summary>
    private static bool IsFocusNone(FormattedTextEntry entry)
        => entry.CommandSet.TryGetValue("focus", out var focus)
           && string.Equals(focus, "none", StringComparison.OrdinalIgnoreCase);

    private static void ApplyPendingCode(
        FormattedTextEntry entry,
        Dictionary<string, string> nameToCode,
        Dictionary<string, string> codeToFacts,
        ref string? pendingCode,
        ref string? pendingFacts,
        bool hasLiveFocus)
    {
        // 优先用立绘焦点锚点：Portraits[PortraitFocus] 反映当前说话者对应的立绘 URL。
        // 对「？？？」等占位名，比 pendingCode（最近一次 chanslot）更可靠。
        // 仅在存在"在场且聚焦"立绘时生效；focus="none" 的画外音对话不走此锚点，
        // 否则 aphris 等背景立绘会被错误当作说话者。
        if (hasLiveFocus)
        {
            var focusCode = ExtractFocusCode(entry);
            if (focusCode != null)
            {
                if (string.IsNullOrEmpty(entry.CharacterCode))
                    entry.CharacterCode = focusCode;
                if (string.IsNullOrEmpty(entry.PicFacts) && codeToFacts.TryGetValue(focusCode, out var focusFacts))
                    entry.PicFacts = focusFacts;
                if (!nameToCode.ContainsKey(entry.CharacterName))
                    nameToCode[entry.CharacterName] = focusCode;
                return;
            }
        }

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

    /// <summary>
    /// 从立绘焦点（Portraits[PortraitFocus]）反查角色 code。
    /// 立绘 URL 形如 ".../avg_4229_aphris_1-2$2.png"，从中提取 "avg_4229_aphris_1"。
    /// </summary>
    private static string? ExtractFocusCode(FormattedTextEntry entry)
    {
        if (entry.Portraits.Count == 0) return null;
        var focus = entry.PortraitFocus;
        if (focus < 0 || focus >= entry.Portraits.Count)
            return null;

        var url = entry.Portraits[focus];
        var fileName = Path.GetFileNameWithoutExtension(url); // avg_4229_aphris_1-2$2
        // 去掉表情/后缀（-N$M 或 -N），保留角色基础 code
        var idx = fileName.IndexOf('-');
        if (idx > 0) fileName = fileName[..idx];
        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }
}
