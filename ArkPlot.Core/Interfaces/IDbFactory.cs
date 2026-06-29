using SqlSugar;

namespace ArkPlot.Core.Interfaces;

/// <summary>
/// 数据库工厂接口。解耦全局单例，支持测试注入。
/// </summary>
public interface IDbFactory
{
    SqlSugarClient GetClient();
}
