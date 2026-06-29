---
name: eliminate-global-singletons
description: 系统性地消除 .NET 项目中的全局单例（static Instance 模式），改为构造函数注入
source: auto-skill
extracted_at: '2026-06-29T08:35:13.345Z'
---

# 消除全局单例指南

## 适用场景

项目中有多个类使用 `ClassName.Instance` 全局单例模式（如 `PrtsAssets.Instance`、`PlotRules.Instance`），导致：
- 测试无法隔离（所有测试共享同一个实例）
- 无法同时运行多个游戏适配器（单例数据互相污染）
- 调用方隐式依赖全局状态，构造函数签名不反映真实依赖

## 步骤

### 1. 找出所有单例引用

```bash
grep -r "\.Instance" --include="*.cs" path/to/project
```

分类统计每个单例的使用点。

### 2. 移除单例模式的 3 个要素

文件中需要修改的部分：

```csharp
// Before: 单例模板
public class MySingleton
{
    private static MySingleton? _instance;
    public static MySingleton Instance => _instance ??= new MySingleton();
    private MySingleton() { }
}
```

```csharp
// After: 可实例化
public class MySingleton
{
    // 删除 _instance 字段
    // 删除 Instance 属性
    public MySingleton() { }  // 构造函数改为 public
}
```

如果还有其他类依赖单例实例化（如 `new PrtsDataProcessor()` 内部调 `PrtsAssets.Instance`），改为接受注入：

```csharp
// After: 依赖注入
public class Consumer
{
    private readonly MySingleton _dep;

    // 保留无参构造向下兼容
    public Consumer() : this(new MySingleton()) { }
    public Consumer(MySingleton dep) { _dep = dep; }
}
```

### 3. 更新适配器提供实例

在 Adapter/工厂类中创建实例并传递：

```csharp
public class MyAdapter : IMyInterface
{
    private readonly Consumer _consumer;

    public MyAdapter() : this(new MySingleton()) { }
    public MyAdapter(MySingleton singleton)
    {
        _consumer = new Consumer(singleton);
    }
}
```

### 4. 处理静态方法中的单例引用

如果实例通过 `new NotificationBlock()` 创建但未被订阅事件，说明调用方不关心通知——创建一次性实例即可：

```csharp
// Before
NotificationBlock.Instance.OnNetErrorHappen(...);

// After
new NotificationBlock().OnNetErrorHappen(...);
```

### 5. 使用点更新清单

| 原始写法 | 修改为 |
|---------|--------|
| `PrtsAssets.Instance.DataImage` | `_assets.DataImage` |
| `PlotRules.Instance` | 构造函数参数 `new PlotRules()` |
| `NotificationBlock.Instance.OnNoMatchTag(...)` | `_notify.OnNoMatchTag(...)` 或 `new NotificationBlock().OnNoMatchTag(...)` |

## 典型模式：数据容器单例（PrtsAssets）

这类单例存储运行期加载的数据字典，消除步骤：

```csharp
// Phase 1: 删除单例模板，构造函数改为 public
public class PrtsAssets
{
    // private static PrtsAssets? _instance;   → 删除
    // public static PrtsAssets Instance => ... → 删除
    public PrtsAssets() { ... }  // 原 private → public
}

// Phase 2: 使用方通过构造函数接收
public class PrtsDataProcessor
{
    public readonly PrtsAssets Res;
    public PrtsDataProcessor() : this(new PrtsAssets()) { }
    public PrtsDataProcessor(PrtsAssets assets) { Res = assets; }
}

// Phase 3: Adapter 创建实例
public class ArknightsResourceResolver
{
    private readonly PrtsAssets _assets;
    public ArknightsResourceResolver() : this(new PrtsAssets()) { }
    public ArknightsResourceResolver(PrtsAssets assets) { _assets = assets; }
}
```

## 典型模式：规则表单例（PlotRules）

```csharp
// Before
public static PlotRules Instance { get; } = new();
private PlotRules() { ... }

// After
public PlotRules() { ... }  // 删 Instance 属性，改 public

// TagProcessor 接受注入
public class TagProcessor
{
    public readonly PlotRules Rules;
    public TagProcessor() : this(new PlotRules(), new NotificationBlock()) { }
    public TagProcessor(PlotRules rules, NotificationBlock? notify = null) { Rules = rules; }
}
```

## 典型模式：事件总线单例（NotificationBlock）

```csharp
// Before
private static readonly Lazy<NotificationBlock> InstanceLazy = new(() => new());
public static NotificationBlock Instance => InstanceLazy.Value;
internal void OnNoMatchTag(...) { ... }

// After: 移除 Lazy/Instance，方法改为 public
public class NotificationBlock
{
    public void OnNoMatchTag(...) { ... }
}
```

## 验证

```bash
# 确认无残留
grep -r "\.Instance" --include="*.cs" path/to/project

# 编译验证
dotnet build path/to/Project.csproj

# 运行测试
dotnet test path/to/Tests.csproj
```

## 注意事项

- 保留无参构造 + `DefaultDbFactory` 或 `new DefaultImpl()` 作为回退，避免破坏现有调用方
- 内部跨类依赖（如 TagProcessor 内部创建 PrtsDataProcessor）也需改为接收注入
- 复杂的单例（初始化需异步加载数据）需要额外处理生命周期