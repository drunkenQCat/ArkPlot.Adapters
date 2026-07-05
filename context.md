# ArkPlot.Adapters 与主仓库 ArkPlot.Core 差异对比

> 生成时间：2026-07-02
> 对比对象：`ArkPlot.Adapters/ArkPlot.Core/`（子模块，适配器架构）vs `ArkPlot.Core/`（主仓库，单体架构）

## 一、架构定位

| | 主仓库 `ArkPlot.Core` | 子模块 `ArkPlot.Adapters/ArkPlot.Core` |
|---|---|---|
| 设计风格 | **单体**：Core 直接包含 Arknights 游戏逻辑 | **适配器**：Core 只含通用逻辑，游戏逻辑下沉到 `ArkPlot.Arknights` |
| 可测试性 | 紧耦合，依赖具体类 | 通过接口解耦，支持 mock 注入 |
| 多游戏支持 | 不支持，Arknights 逻辑硬编码在 Core 中 | 支持，通过 `IScriptParser` / `IStoryDataProvider` / `ITagRenderer` / `IResourceResolver` 四接口注入 |

## 二、接口层差异（Interfaces/）

主仓库仅有 **1 个**接口，子模块有 **8 个**：

| 接口 | 主仓库 | 子模块 | 说明 |
|---|---|---|---|
| `ILoudnessNormalizer` | ✅ | ✅ | 共有 |
| `IStoryDataProvider` | ❌ | ✅ | 剧情数据下载（从远程源获取元数据和原始脚本） |
| `IScriptParser` | ❌ | ✅ | 脚本解析（原始文本 → `ScriptLine` 序列） |
| `ITagRenderer` | ❌ | ✅ | 标签渲染（`ScriptLine` → Markdown） |
| `IResourceResolver` | ❌ | ✅ | 资源 URL 解析（角色代码/背景/音频 → 可下载 URL） |
| `INetworkClient` | ❌ | ✅ | HTTP 客户端抽象（解耦 `NetworkUtility`，支持测试 mock） |
| `IImageDescriber` | ❌ | ✅ | 图片描述服务（Vision 散文描述 + YAML 事实提取） |
| `IDbFactory` | ❌ | ✅ | 数据库工厂抽象（解耦全局单例 `DbFactory`） |

## 三、模型层差异（Model/）

### 子模块新增模型

| 模型 | 说明 |
|---|---|
| `ScriptLine` | **通用中间表示**，替代主仓库的 `FormattedTextEntry`。所有游戏适配器共享此基类，可独立持久化到 DB |
| `StringDict` | 带变更通知（`OnChanged` 事件）的有序字典，修改时自动序列化回 JSON 列 |
| `PlotStatus` | Plot 处理状态枚举 |

### 主仓库独有模型（子模块已移除）

| 模型 | 移除原因 |
|---|---|
| `FormattedTextEntry` | 被 `ScriptLine` 替代（通用化命名） |
| `Alias` | Arknights 特有逻辑 |
| `PlotRules` | Arknights 特有逻辑 |
| `PrtsAssets` | Arknights 特有，移至 `ArkPlot.Arknights.Data` |
| `PrtsData` | Arknights 特有，移至 `ArkPlot.Arknights.Data` |
| `PrtsPortraitLink` | Arknights 特有，移至 `ArkPlot.Arknights.Data` |
| `PrtsResource` | Arknights 特有，移至 `ArkPlot.Arknights.Data` |
| `SentenceMethod` | Arknights 特有，移至 `ArkPlot.Arknights.Data` |

### 关键变更：`Plot.cs`

```diff
- public List<FormattedTextEntry> Entries { get; set; }       // 主仓库
+ public List<ScriptLine> Entries { get; set; }               // 子模块

- public List<FormattedTextEntry> TextVariants { get; set; }  // 主仓库
+ public List<ScriptLine> TextVariants { get; set; }          // 子模块
```

## 四、基础设施层差异（Infrastructure/）

| 文件 | 主仓库 | 子模块 | 说明 |
|---|---|---|---|
| `AppPaths` | ✅ | ✅ | 共有 |
| `DbFactory` | ✅ | ✅ | 共有（静态单例 `SqlSugarClient`） |
| `DefaultDbFactory` | ❌ | ✅ | `IDbFactory` 的默认实现，委托给静态 `DbFactory`，便于 DI |
| `ImageCachePaths` | ✅ | ✅ | 共有 |
| `OutputPaths` | ✅ | ✅ | 共有 |
| `TtsCachePaths` | ✅ | ✅ | 共有 |
| `VideoCachePaths` | ✅ | ✅ | 共有 |

## 五、工作流层差异（Utilities/WorkFlow/）

### 子模块新增

| 文件 | 说明 |
|---|---|
| `StoryPipeline.cs` | **通用剧情处理管线**，串联 数据获取→解析→渲染→图片描述→文档构建。不依赖任何特定游戏逻辑，所有游戏特性通过接口注入 |

### 主仓库独有（子模块已移至 ArkPlot.Arknights）

| 文件 | 说明 |
|---|---|
| `AkpParser.cs` | Arknights 脚本解析（移至 `ArkPlot.Arknights/Workflow/`） |
| `AkpStoryLoader.cs` | Arknights 故事加载器（移至 `ArkPlot.Arknights/Workflow/`） |

### StoryDocument 渲染器差异

| 文件 | 主仓库 | 子模块 | 说明 |
|---|---|---|---|
| `StoryDocumentBuilder` | ✅ | ✅ | 共有 |
| `StoryDocumentContext` | ✅ | ✅ | 共有 |
| `OutputMode` | ✅ | ✅ | 共有 |
| `IMdRenderer` | ✅ | ✅ | 共有 |
| `ReadableRenderer` | ✅ | ✅ | 共有 |
| `PromptRenderer` | ✅ | ✅ | 共有 |
| `PromptRendererConfig` | ❌ | ✅ | 子模块新增，Prompt 渲染器配置类 |

### 分析组件差异（WorkFlow/StoryDocument/Analysis/）

三个文件（`PicDescEnricher`, `PortraitProcessor`, `SegmentGrouper`）两边共有，无差异。

## 六、Utilities 层其他差异

### 主仓库独有（子模块已移至 ArkPlot.Arknights）

| 目录/文件 | 移除原因 |
|---|---|
| `Utilities/PrtsComponents/` (4 文件) | Arknights PRTS 解析逻辑，移至 `ArkPlot.Arknights/Parsing/` |
| `Utilities/TagProcessingComponents/` (4 文件) | Arknights 标签处理逻辑，移至 `ArkPlot.Arknights/TagProcessing/` |
| `Utilities/AkpProcess.cs` | Arknights 处理流程入口，移至 `ArkPlot.Arknights/Workflow/` |
| `Data/ArkPlotRegs.cs` | Arknights 正则规则，移至 `ArkPlot.Arknights/Parsing/` |

### 两边共有

`GitHubProxy`, `HtmlTagParser`, `NetworkUtility`, `StringExtension`, `ArknightsDbComponents/ReviewTableParser`, `TypstComponents/TypstRenderer`, `TypstComponents/TypstTranslator`

## 七、csproj 差异

| 属性 | 主仓库 | 子模块 |
|---|---|---|
| `TargetFramework` | net9.0 | net9.0 |
| `Version` | 1.1.3 | 1.1.3 |
| `InternalsVisibleTo` | `ArkPlot.Avalonia.Tests` (1 个) | `ArkPlot.Avalonia.Tests`, `ArkPlot.Core.Tests`, `ArkPlot.Arknights` (3 个) |
| `ProjectReference` | `ArkPlot.Vision` | 无 |
| NuGet 包 | 相同 5 个 | 相同 5 个 |

关键差异：子模块 **不再引用 `ArkPlot.Vision`**，图片描述能力通过 `IImageDescriber` 接口抽象，由调用方注入实现。

## 八、总结：子模块做了什么

子模块 `ArkPlot.Adapters/ArkPlot.Core` 对主仓库 `ArkPlot.Core` 做了 **三项核心重构**：

1. **游戏逻辑剥离**：将所有 Arknights 特有代码（PRTS 解析、标签处理、AkpProcess、ArkPlotRegs 等）从 Core 移出到独立的 `ArkPlot.Arknights` 项目。Core 变为纯通用引擎。

2. **接口抽象层**：新增 7 个接口（`IStoryDataProvider`, `IScriptParser`, `ITagRenderer`, `IResourceResolver`, `INetworkClient`, `IImageDescriber`, `IDbFactory`），使 Core 可通过依赖注入支持任意游戏适配器和测试 mock。

3. **通用管线 + 通用模型**：新增 `StoryPipeline`（串联全流程的通用管线）和 `ScriptLine`（替代 `FormattedTextEntry` 的通用中间表示），让 Core 不再绑定特定游戏的数据结构。

## 九、待补：Arknights 建表逻辑

Adapters Core 的 `DbFactory` 只注册了通用表（`ScriptLine` 等），不感知 Arknights 特有表。`FormattedTextEntry` 采用 **TPC（Table Per Concrete Type）** 继承映射 — 展平到一张表含全部 21 个字段，与旧设计完全兼容。

但 Arknights 项目需要自己的建表初始化逻辑，注册以下 4 张表：

```csharp
// ArkPlot.Arknights 中需要补上（如 ArknightsDbInitializer.Init()）
CodeFirst.InitTables(
    typeof(FormattedTextEntry),   // TPC 展平，含基类+子类全部字段
    typeof(PrtsData),
    typeof(PrtsResource),
    typeof(PrtsPortraitLink)
);
```

**影响**：从旧数据库迁移无问题（表已存在）；生产环境从零建库时会 `no such table: FormattedTextEntry`。
