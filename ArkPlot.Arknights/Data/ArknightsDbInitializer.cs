using SqlSugar;

namespace ArkPlot.Arknights;

/// <summary>
/// Arknights 适配器建表初始化器。
/// Core DbFactory 只建通用表（Act/StoryChapter/Plot/SyncState/PicDescription），
/// Arknights 特有表由此处注册。
/// </summary>
public static class ArknightsDbInitializer
{
    private static bool _initialized;

    /// <summary>
    /// 在给定 client 上创建 Arknights 特有表。
    /// 幂等：SqlSugar InitTables 对已存在的表只补列不删数据。
    /// </summary>
    public static void Init(SqlSugarClient? db = null)
    {
        db ??= Core.Infrastructure.DbFactory.GetClient();

        db.CodeFirst.SetStringDefaultLength(200).InitTables(
            typeof(FormattedTextEntry),
            typeof(PrtsData),
            typeof(PrtsResource),
            typeof(PrtsPortraitLink)
        );

        _initialized = true;
    }

    /// <summary>是否已执行过 Init（进程级标志，测试间共享）。</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>测试用：重置标志。</summary>
    public static void Reset() => _initialized = false;
}
