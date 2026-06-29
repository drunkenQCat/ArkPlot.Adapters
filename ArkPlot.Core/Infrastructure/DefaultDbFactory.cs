using ArkPlot.Core.Interfaces;
using SqlSugar;

namespace ArkPlot.Core.Infrastructure;

/// <summary>
/// IDbFactory 的默认实现，委托给静态 DbFactory。
/// 便于通过依赖注入传递数据库客户端。
/// </summary>
public class DefaultDbFactory : IDbFactory
{
    public SqlSugarClient GetClient() => DbFactory.GetClient();
}
