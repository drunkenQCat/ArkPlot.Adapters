using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;
using SqlSugar;

namespace ArkPlot.Core.Services;

/// <summary>
/// Plot 缓存层：处理完成的章节可写库复用，避免重复下载和解析。
/// 泛型 T 允许不同游戏使用各自的 ScriptLine 子类（如 FormattedTextEntry）。
/// </summary>
public static class PlotCache<T> where T : ScriptLine, new()
{
    /// <summary>
    /// 查询某活动下已缓存的章节标题。
    /// </summary>
    public static async Task<HashSet<string>> GetCachedTitlesAsync(long actId, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        var titles = await db.Queryable<Plot>()
            .Where(p => p.ActId == actId && p.Status == (int)PlotStatus.Parsed)
            .Select(p => p.Title)
            .ToListAsync();
        return new HashSet<string>(titles);
    }

    /// <summary>
    /// 尝试从缓存加载一个章节的条目。
    /// 返回 (Plot, List{T})，缓存未命中则返回 null。
    /// </summary>
    public static async Task<(Plot Plot, List<T> Entries)?> TryLoadAsync(long actId, string title, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        var plot = await db.Queryable<Plot>()
            .FirstAsync(p => p.ActId == actId && p.Title == title && p.Status == (int)PlotStatus.Parsed);
        if (plot == null) return null;

        var entries = await db.Queryable<T>()
            .Where(e => e.PlotId == plot.Id)
            .OrderBy(e => e.Index)
            .ToListAsync();

        return (plot, entries);
    }

    /// <summary>
    /// 将章节写入缓存（upsert）。
    /// status=1 表示仅下载未解析，status=2 表示解析完成可直接使用。
    /// 当 StoryChapterId > 0 时按 (ActId, StoryChapterId) 匹配现有记录，
    /// 有则更新 Status + 替换条目，无则 INSERT。
    /// StoryChapterId = 0 时保持旧 INSERT 行为（兼容旧数据路径）。
    /// </summary>
    public static async Task SaveAsync(Plot plot, List<T> entries, PlotStatus status = PlotStatus.Parsed, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();
        plot.Status = (int)status;

        if (plot.StoryChapterId > 0)
        {
            var existing = await db.Queryable<Plot>()
                .FirstAsync(p => p.ActId == plot.ActId && p.StoryChapterId == plot.StoryChapterId);

            if (existing != null)
            {
                await db.Deleteable<T>()
                    .Where(e => e.PlotId == existing.Id).ExecuteCommandAsync();

                existing.Status = (int)status;
                await db.Updateable(existing).ExecuteCommandAsync();

                foreach (var entry in entries)
                    entry.PlotId = existing.Id;
                await db.Insertable(entries).ExecuteCommandAsync();
                return;
            }
        }

        var plotId = db.Insertable(plot).ExecuteReturnIdentity();
        foreach (var entry in entries)
            entry.PlotId = plotId;
        await db.Insertable(entries).ExecuteCommandAsync();
    }

    /// <summary>
    /// 清理指定活动下所有"空内容"的脏缓存（Status=2 但条目全为空）。
    /// </summary>
    public static async Task<int> CleanupEmptyPlotsAsync(long actId, SqlSugarClient? db = null)
    {
        db ??= DbFactory.GetClient();

        var emptyPlots = await db.Queryable<Plot>()
            .Where(p => p.ActId == actId && p.Status == (int)PlotStatus.Parsed)
            .ToListAsync();

        var cleanedCount = 0;
        foreach (var plot in emptyPlots)
        {
            var entries = await db.Queryable<T>()
                .Where(e => e.PlotId == plot.Id)
                .ToListAsync();

            if (entries.Count == 0 || entries.All(e => string.IsNullOrWhiteSpace(e.OriginalText)))
            {
                await db.Deleteable<T>()
                    .Where(e => e.PlotId == plot.Id).ExecuteCommandAsync();
                await db.Deleteable<Plot>()
                    .Where(p => p.Id == plot.Id).ExecuteCommandAsync();
                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
            Console.WriteLine($"[PlotCache] 已清理 {cleanedCount} 条空内容脏缓存 (ActId={actId})");

        return cleanedCount;
    }
}
