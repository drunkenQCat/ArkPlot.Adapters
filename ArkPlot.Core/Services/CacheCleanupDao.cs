using System.IO;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Core.Services;

/// <summary>
/// 缓存清理 DAO：按「章节」粒度清除图片描述缓存（DB）与小说化缓存（文件 + json）。
/// 设计前提：
///  - PicDescription 无章节字段，但章节的 ScriptLine 条目携带了它用到的全部图片信息
///    （CharacterCode = 立绘 DedupKey，ResourceUrls = 场景/立绘/背景的图片 URL），可据此还原该章节的缓存 key 集合。
///  - 小说化缓存 .novelizer-cache.json 的 key 为「源 md 完整路径 :: 模型」，源 md 路径含章节名，可按章节前缀精确删。
/// </summary>
public static class CacheCleanupDao
{
    /// <summary>
    /// 按章节删除图片描述缓存。
    /// 收集章节所有条目的 CharacterCode（去 # 后缀）与图片 URL，删除 DedupKey 或 ImageUrl 命中
    /// 且 Source = Vision 的记录（Placeholder/Error 保留，语义为可重试）。
    /// </summary>
    /// <param name="entries">该章节的所有条目。</param>
    /// <returns>删除的缓存记录数。</returns>
    public static int DeletePicDescriptions(IEnumerable<ScriptLine> entries, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();

        var dedupKeys = new HashSet<string>(StringComparer.Ordinal);
        var imageUrls = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.CharacterCode))
            {
                var code = entry.CharacterCode;
                var hashIdx = code.IndexOf('#');
                if (hashIdx >= 0) code = code[..hashIdx];
                dedupKeys.Add(code);
            }

            foreach (var url in entry.ResourceUrls)
            {
                if (!string.IsNullOrEmpty(url))
                    imageUrls.Add(url);
            }
        }

        if (dedupKeys.Count == 0 && imageUrls.Count == 0)
            return 0;

        // DedupKey 命中（立绘 code 或场景 URL），或 ImageUrl 命中（同一 URL 被多个 code 复用缓存的情况）。
        // SqlSugar 表达式树对 HashSet.Contains 翻译不可靠，转 List 保证生成 IN 子句。
        var keys = dedupKeys.ToList();
        var urls = imageUrls.ToList();
        var count = db.Deleteable<PicDescription>()
            .Where(it => it.Source == "Vision" &&
                         (keys.Contains(it.DedupKey) || urls.Contains(it.ImageUrl)))
            .ExecuteCommand();

        Console.WriteLine($"[CacheCleanupDao] 删除图片描述缓存 {count} 条"
                          + $"（DedupKey {dedupKeys.Count} / ImageUrl {imageUrls.Count}）");
        return count;
    }

    /// <summary>
    /// 按章节删除小说化缓存。
    /// 从 <see cref="storyOutputDir"/>.novelizer-cache.json 中删除「源 md 路径以 {storyOutputDir}/{章节名}.md 开头」的条目，
    /// 并删除对应的 <c>*_novel_*.md</c> 输出文件。章节未处理的缓存不受影响。
    /// </summary>
    /// <param name="storyOutputDir">当前故事输出目录（如 …/output/孤星）。</param>
    /// <param name="chapterNames">要清除缓存的章节名。</param>
    /// <returns>删除的小说化输出文件数与缓存条目数（以元组返回）。</returns>
    public static (int EntriesRemoved, int FilesDeleted) DeleteNovelizerCache(
        string storyOutputDir,
        IEnumerable<string> chapterNames)
    {
        var names = chapterNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (names.Count == 0 || !Directory.Exists(storyOutputDir))
            return (0, 0);

        var cacheFile = Path.Combine(storyOutputDir, ".novelizer-cache.json");

        // 1. 从 json 中删除匹配章节的条目
        var entriesRemoved = 0;
        if (File.Exists(cacheFile))
        {
            try
            {
                var json = File.ReadAllText(cacheFile);
                var entries = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, object>>(json) ?? new();

                var keysToRemove = entries.Keys
                    .Where(key => names.Any(name =>
                        key.StartsWith(Path.Combine(storyOutputDir, name) + ".md::", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var key in keysToRemove)
                    entries.Remove(key);

                entriesRemoved = keysToRemove.Count;
                File.WriteAllText(cacheFile,
                    System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CacheCleanupDao] 读取小说缓存失败：{ex.Message}");
            }
        }

        // 2. 删除匹配章节的小说化输出文件（无需源 md 存在——novel 文件可能比源 md 更「新鲜」）
        var filesDeleted = 0;
        foreach (var name in names)
        {
            foreach (var f in Directory.GetFiles(storyOutputDir, $"{name}_novel_*.md"))
            {
                try { File.Delete(f); filesDeleted++; }
                catch { /* 占用跳过 */ }
            }
        }

        Console.WriteLine($"[CacheCleanupDao] 删除小说缓存 {entriesRemoved} 条缓存记录、{filesDeleted} 个输出文件");
        return (entriesRemoved, filesDeleted);
    }
}