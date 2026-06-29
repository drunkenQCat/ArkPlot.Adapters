---
name: testing-dotnet-sqlite-project
description: 为依赖 SqlSugar(SQLite) ORM、Singleton 基础设施和外部 DLL 引用的 .NET 项目编写全面测试，包括连接生命周期和并行执行处理
source: auto-skill
extracted_at: '2026-06-29T03:52:29.217Z'
---

# .NET SQLite 项目测试编写指南

## 适用场景

目标项目具有以下部分或全部特征：
- 使用 **SqlSugar** (或类似 ORM) + **SQLite** 做数据持久化
- 核心基础设施是 **static Singleton**（`DbFactory`、`*Paths` 等）
- **Model 源文件**可能位于另一目录（需链接或拷贝）
- 存在跨项目引用（如 `ArkPlot.Vision`、`ArkPlot.Core`）
- `InternalsVisibleTo` 只给了部分测试项目

## 第一步：项目基础设施检查

在写任何测试代码之前，先确保构建能通过：

### 1.1 添加 InternalsVisibleTo

```xml
<!-- 在 ArkPlot.Core.csproj 中添加 -->
<ItemGroup>
  <InternalsVisibleTo Include="ArkPlot.Core.Tests" />
</ItemGroup>
```

启用后，测试项目可以访问 `internal` 类型（如 `PlotRegsBasicHelper`、`IMdRenderer`）。

### 1.2 补充缺失的源文件

如果 Model 源文件在另一个目录（如原项目），使用 **Compile 链接**而非拷贝：

```xml
<ItemGroup>
  <Compile Include="..\..\ArkPlot\ArkPlot.Core\Model\**\*.cs" Link="Model\%(RecursiveDir)%(FileName)%(Extension)" />
</ItemGroup>
```

### 1.3 修正跨项目引用路径

跨工作区的 ProjectReference 需要用正确相对路径：

```xml
<ProjectReference Include="..\..\ActualPath\ArkPlot.Vision\ArkPlot.Vision.csproj" />
```

### 1.4 标记测试需拷贝的静态文件

```xml
<ItemGroup>
  <None Update="Prefix\arkplot.db">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## 第二步：代码扫描与分层

用子代理（Explore agent）扫描整个代码库，按以下维度对每个类做评估：

| 测试难度 | 特征 | 示例 |
|---------|------|------|
| **纯逻辑** | 纯字符串/正则/路径操作，无外部依赖 | `StringExtensions`、`HtmlTagParser`、`TypstTranslator` |
| **需要 DB** | 使用 `DbFactory.GetClient()`、接受可选 `SqlSugarClient?` | `PlotCache`、`StorySyncService` |
| **需要委托注入** | 构造函数接受 Func/委托参数 | `PicDescService` |
| **需要 Singleton 管理** | 静态单例、全局可变状态 | `PrtsAssets`、`PlotRules`、`GitHubProxy` |
| **不可单元测试** | 构造函数直接网络下载、文件系统重度耦合 | `ReviewTableParser`、`NetworkUtility` |

## 第三步：数据库测试基础设施

### 3.1 用临时文件代替 :memory:

**不要用 `Data Source=:memory:`**。SqlSugar 的 `IsAutoCloseConnection` 在内存模式下行为复杂，易导致"connection disposed"或"no such table"错误。

改为**每次测试用独立的临时文件**：

```csharp
public class MyDbTests : IDisposable
{
    private readonly string _dbPath;

    public MyDbTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"arkplot_test_{Guid.NewGuid():N}.db");
        DbFactory.ConfigureForTesting($"Data Source={_dbPath}");
        // 后续调用 GetClient() 会基于 _dbPath 创建新库
    }

    public void Dispose()
    {
        DbFactory.Reset();
        try { File.Delete(_dbPath); } catch { }
    }
}
```

### 3.2 禁用并行执行

xUnit 默认跨类并行测试。所有修改 `DbFactory` 的类必须共享同一个 `[Collection]`：

```csharp
// 定义一个 Collection Fixture（放在一个文件中）
[CollectionDefinition("DbTests", DisableParallelization = true)]
public class DbTestsCollection : ICollectionFixture<DbTestFixture>;

public class DbTestFixture : IDisposable
{
    public void Dispose()
    {
        DbFactory.Reset();
    }
}

// 每个 DB 测试类加上
[Collection("DbTests")]
public class MyDbTests : IDisposable { ... }
```

**所有修改 `DbFactory`、`GitHubProxy.Prefix`、`PrtsAssets` 等静态状态的测试类都要加 `[Collection]`。**

## 第四步：按难度递增编写测试

### 4.1 纯逻辑测试

没有依赖，直接实例化并断言：

```csharp
[Fact]
public void ToCommandSet_SinglePair()
{
    var result = "name=测试".ToCommandSet();
    Assert.Single(result);
    Assert.Equal("测试", result["name"]);
}
```

### 4.2 DB 测试

用第三步的临时文件模板，注入 `SqlSugarClient?` 到目标方法（`PlotCache`、`StorySyncService` 都支持可选 `db` 参数）：

```csharp
[Fact]
public async Task SaveAsync_Roundtrip()
{
    var plot = new Plot { ActId = 1, Title = "测试", StoryChapterId = 10 };
    await PlotCache.SaveAsync(plot, entries, status: 2, _db);
    var result = await PlotCache.TryLoadAsync(1, "测试", _db);
    Assert.NotNull(result);
}
```

### 4.3 委托注入测试

使用 lambda 作为 mock，验证调用次数和缓存行为：

```csharp
var callCount = 0;
using var svc = new PicDescService(
    describeByUrl: url =>
    {
        callCount++;
        return Task.FromResult("描述");
    });
var r1 = await svc.GetOrCreatePicDescAsync("url.png");
var r2 = await svc.GetOrCreatePicDescAsync("url.png");
Assert.Equal(1, callCount); // 第二次命中缓存
```

### 4.4 Prefix 数据库测试

拷贝前置数据库（`Prefix/arkplot.db`）到临时文件，**只读模式**避免污染：

```csharp
var srcPath = Path.Combine(AppContext.BaseDirectory, "Prefix", "arkplot.db");
if (File.Exists(srcPath))
{
    var tmpPath = Path.Combine(Path.GetTempPath(), $"arkplot_prefix_{Guid.NewGuid():N}.db");
    File.Copy(srcPath, tmpPath, overwrite: true);
    DbFactory.ConfigureForTesting($"Data Source={tmpPath}");
}
```

## 第五步：断言原则

- **路径测试不依赖特定分隔符**：用 `Assert.Contains` 替代 `Assert.Equal` 对完整路径断言，避免 Windows (`\`) vs Unix (`/`) 差异
- **捕获真实行为**：先用一个简单 Fact 观察实际输出，再用更松的断言固定行为
- **检查副作用而非实现细节**：对缓存测试，验证调用计数而非具体返回值格式

## 第六步：调试技巧

| 症状 | 原因 | 修复 |
|------|------|------|
| `no such table: main.Plot` | 连接生命周期问题 | 改用临时文件 DB |
| `Cannot access a disposed object` | 静态 DB 被并发 Reset | 加 `[Collection]` 禁用并行 |
| `ExecuteScalar only when connection is open` | `IsAutoCloseConnection` 配置冲突 | 检查 DbFactory 配置，用文件 DB |
| 构建失败 `CS0234: namespace...Model` | Model 源文件缺失 | 用 Compile Link 引入 |

## 附录：SqlSugar ORM 继承的特殊处理

当你将现有模型拆分为"基类（通用字段）+ 子类（游戏特有字段）"时，SqlSugar 的 CodeFirst 建表需要特别注意。

### 问题说明

SqlSugar 扫描实体属性时只认本类直接声明的 `[SugarColumn]`。如果基类属性没有标记，即使子类用 `new` 重新声明，CodeFirst 也可能跳过该列。

### 解决方案

在子类中用 `new` 关键字重写基类属性，并在子类上加 `[SugarColumn]` 属性：

```csharp
// 基类（无 SugarColumn 标记）
public class ScriptLine
{
    public string OriginalText { get; set; } = "";
    public string MdText { get; set; } = "";
    public StringDict CommandSet { get; set; } = new();
    public List<string> ResourceUrls { get; set; } = new();
    // ...
}

// 子类（加 SqlSugar 属性表标记）
[SugarTable("FormattedTextEntry")]
public class FormattedTextEntry : ScriptLine
{
    // 用 new + 属性访问器重写，附加 SugarColumn
    [SugarColumn(Length = 1000)]
    public new string OriginalText { get => base.OriginalText; set => base.OriginalText = value; }

    [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
    public new StringDict CommandSet { get => base.CommandSet; set => base.CommandSet = value; }

    [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
    public new List<string> ResourceUrls { get => base.ResourceUrls; set => base.ResourceUrls = value; }

    // 方舟特有字段
    public int PortraitFocus { get; set; }
    public string Bg { get; set; } = "";
}
```

这样 CodeFirst 看到子类声明的 `[SugarColumn]` 属性的字段，就会正确建表。基类属性用于跨游戏通用逻辑访问。

### 复制构造函数

子类需要调用基类的 `CopyFrom` 方法：

```csharp
public class ScriptLine
{
    protected void CopyFrom(ScriptLine source)
    {
        OriginalText = source.OriginalText;
        MdText = source.MdText;
        // ...
    }
}

public class FormattedTextEntry : ScriptLine
{
    public FormattedTextEntry(FormattedTextEntry entry)
    {
        CopyFrom(entry);  // 复制基类字段
        PortraitFocus = entry.PortraitFocus; // 复制子类特有字段
        Bg = entry.Bg;
    }
}
```

## 测试文件组织建议

```
ArkPlot.Core.Tests/
├── DbTestFixture.cs              # CollectionDefinition + Fixture
├── StringExtensionsTests.cs      # 纯逻辑（16 tests）
├── HtmlTagParserTests.cs         # 纯逻辑（7 tests）
├── PathTests.cs                  # 纯逻辑（16 tests）
├── TypstTranslatorTests.cs       # 纯逻辑（7 tests）
├── GitHubProxyExtendedTests.cs   # 纯逻辑 + Singleton（13 tests）
├── SegmentGrouperTests.cs        # 纯逻辑（7 tests）
├── StoryDocumentBuilderTests.cs  # 纯逻辑（9 tests）
├── PortraitProcessorTests.cs     # 纯逻辑（8 tests）
├── StorySyncServiceTests.cs      # DB（12 tests）
├── PlotCacheTests.cs             # DB（7 tests）
└── PicDescServiceTests.cs        # DB + 委托注入（13 tests）
```

纯逻辑测试（无 DB/网络/文件系统依赖）应该优先编写，它们最快、最稳定，也是重构时最高的安全保障。