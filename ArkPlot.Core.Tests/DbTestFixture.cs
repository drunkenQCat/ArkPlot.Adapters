using ArkPlot.Core.Infrastructure;
using SqlSugar;
using Xunit;

[CollectionDefinition("DbTests", DisableParallelization = true)]
public class DbTestsCollection : ICollectionFixture<DbTestFixture>;

public class DbTestFixture : IDisposable
{
    public SqlSugarClient CreateMemoryDb()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        return DbFactory.GetClient();
    }

    public void Reset()
    {
        DbFactory.Reset();
    }

    public void Dispose()
    {
        DbFactory.Reset();
    }
}
