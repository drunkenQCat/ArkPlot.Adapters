# ArkPlot.Core 项目遗嘱

> **最后更新**: 2026-06-29  
> **维护者**: @ArkPlot.Core/  
> **目的**: 帮助新 Agent 快速理解本项目的架构、组件职责和关键设计决策

---

## 📋 项目概述

ArkPlot.Core 是明日方舟剧情解析与重构系统的**核心业务逻辑层**，负责：

1. **数据获取**: 从 PRTS Wiki 下载剧情原始数据
2. **数据解析**: 解析明日方舟特有的标签语法（`[name="xxx"]`、`[charslot]`、`[background]` 等）
3. **数据转换**: 将原始数据转换为 Markdown、Typst、HTML 等多种格式
4. **图片描述**: 调用视觉模型（百炼/Ollama）生成图片的散文描述和结构化视觉事实
5. **数据持久化**: 使用 SqlSugar ORM 管理 SQLite 数据库

**目标框架**: .NET 9.0  
**项目类型**: Class Library (AnyCpu)  
**文档数量**: 60 个源文件

---

## 🗂️ 目录结构

```
ArkPlot.Core/
├── Data/                    # 静态数据和配置
├── Infrastructure/          # 基础设施（数据库、路径管理）
├── Interfaces/              # 接口定义
├── Model/                   # 数据模型（POCO）
├── Services/                # 业务服务
└── Utilities/               # 工具类（按功能域组织）
    ├── AkpProcess.cs
    ├── ArknightsDbComponents/
    ├── GitHubProxy.cs
    ├── HtmlTagParser.cs
    ├── NetworkUtility.cs
    ├── PrtsComponents/      # PRTS 数据处理
    ├── StringExtension.cs
    ├── TagProcessingComponents/  # 标签解析
    ├── TypstComponents/     # Typst 渲染
    └── WorkFlow/            # 工作流编排
```

---

## 📦 核心组件详解

### 1️⃣ Model/ — 数据模型

**设计原则**: 纯 POCO + SqlSugar 特性，不含业务逻辑

#### 关键模型

| 模型 | 职责 | 数据库表 |
|------|------|---------|
| `FormattedTextEntry` | 格式化文本条目，包含原始文本、Markdown、Typst 等多种格式 | FormattedTextEntry |
| `Plot` | 剧情章节（对应一个 Story Chapter） | Plot |
| `StoryChapter` | 章节元数据 | StoryChapter |
| `Act` | 游戏活动（如"孤星"、"水晶箭行动"） | Act |
| `PicDescription` | 图片描述缓存 | PicDescription |
| `PrtsData` | PRTS 原始数据 | PrtsData |
| `PrtsResource` | PRTS 资源（图片、音频） | PrtsResource |
| `PrtsPortraitLink` | 立绘链接映射 | PrtsPortraitLink |
| `SyncState` | 同步状态 | SyncState |

#### FormattedTextEntry 详解

这是系统中最核心的模型，表示一行格式化后的剧情文本：

```csharp
public class FormattedTextEntry
{
    public long Id { get; set; }              // 主键
    public long PlotId { get; set; }          // 所属章节
    public int Index { get; set; }            // 行号
    
    // 多格式文本
    public string OriginalText { get; set; }  // 原始文本（含标签）
    public string MdText { get; set; }        // Markdown 格式
    public string TypText { get; set; }       // Typst 格式
    
    // 解析结果
    public string Type { get; set; }          // 标签类型（dialog, charslot, background 等）
    public StringDict CommandSet { get; set; } // 命令参数（JSON）
    public string CharacterName { get; set; } // 角色名
    public string? CharacterCode { get; set; } // 角色代码（如 char_220_grani）
    public string Dialog { get; set; }        // 对话内容
    public string Bg { get; set; }            // 背景图 URL
    public List<string> Portraits { get; set; } // 立绘图 URL 列表
    public int PortraitFocus { get; set; }    // 立绘焦点（-1=无, 0=单人居中, 1=双人左, 2=三人右）
    
    // 图片描述
    public string PicDesc { get; set; }       // 散文描述
    public string PicFacts { get; set; }      // YAML 结构化事实（运行时填充，不持久化）
    
    // 瞬态标记
    public bool SkipPortraitOutput { get; set; } // 跳过立绘输出（如 charslot focus="none"）
}
```

**关键设计决策**:
- `CommandSet` 使用 `StringDict`（继承自 `Dictionary<string, string>`），SqlSugar 自动序列化为 JSON
- `PicFacts` 标记为 `[SugarColumn(IsIgnore = true)]`，运行时从 `PicDescription` 表填充
- 提供复制构造函数，便于深拷贝

---

### 2️⃣ Infrastructure/ — 基础设施层

#### DbFactory.cs — 数据库工厂

**职责**: 提供 SqlSugar 单例客户端，自动建表

```csharp
public static class DbFactory
{
    public static SqlSugarClient GetClient()
    {
        // 1. 单例模式
        // 2. 使用 System.Text.Json 替代 Newtonsoft.Json
        // 3. CodeFirst 自动建表
        // 4. 支持测试用内存数据库
    }
}
```

**关键设计**:
- 使用 `SystemTextJsonSerializer` 让 SqlSugar 的 `IsJson=true` 使用 `System.Text.Json`
- `CodeFirst.InitTables()` 自动创建/更新所有模型对应的表
- 提供 `ConfigureForTesting()` 方法支持单元测试使用内存数据库

#### 路径管理类

| 文件 | 职责 |
|------|------|
| `AppPaths.cs` | 应用程序根路径、数据目录 |
| `ImageCachePaths.cs` | 图片缓存路径（`Data/PicCache/`） |
| `OutputPaths.cs` | 输出文件路径（`output/{actName}/`） |
| `TtsCachePaths.cs` | TTS 音频缓存路径 |
| `VideoCachePaths.cs` | 视频缓存路径 |

---

### 3️⃣ Services/ — 业务服务层

#### PicDescService.cs — 图片描述服务

**职责**: 生成和缓存图片描述（散文 + YAML 结构化事实）

**工作流程**:
```
1. 检查数据库缓存 → 命中则直接返回
2. 未命中 → 调用视觉模型（百炼/Ollama）生成散文描述
3. 散文描述 → 调用 LLM 提取 YAML 结构化事实
4. 写入数据库缓存（PicDescription 表）
5. 清理临时下载的图片文件
```

**关键设计**:
- **两级缓存查找**:
  - 第一级：按 `DedupKey`（立绘用 CharacterCode，场景用 imageUrl）
  - 第二级：按 `ImageUrl`（同一 URL 可能对应多个 CharacterCode）
- **Debug 模式**: 强制跳过缓存，重新生成
- **干扰图片过滤**: 硬编码跳过 `avg_bg_bg_black.png`、`avg_char_empty.png` 等占位图
- **非图片过滤**: MP3/OGG/WAV 等音频文件直接返回空字符串，不下载、不描述、不入库

**YAML 提取提示词**: 定义了场景模板和角色模板，根据输入内容自动判断使用哪个模板

```csharp
public record PicDescResult(string Description, string? Facts);

public async Task<PicDescResult> GetOrCreatePicDescWithFactsAsync(
    string imageUrl, 
    string? characterCode = null)
```

#### PlotCache.cs — 剧情缓存服务

**职责**: 缓存已下载和解析的剧情章节，避免重复下载和解析

**缓存策略**:
- `Status = 1`: 已下载但未解析
- `Status = 2`: 已解析完成（可用）

**关键方法**:
```csharp
// 获取已缓存的章节标题列表
public static async Task<HashSet<string>> GetCachedTitlesAsync(long actId)

// 尝试加载已缓存的章节
public static async Task<(Plot Plot, List<FormattedTextEntry> Entries)?> 
    TryLoadAsync(long actId, string title)

// 保存章节到缓存
public static async Task SaveAsync(Plot plot, List<FormattedTextEntry> entries)

// 清理空内容脏缓存
public static async Task CleanupEmptyPlotsAsync(long actId)
```

#### NotificationBlock.cs — 事件通知

**职责**: 集中管理事件通知（如下载进度、解析错误、标签未匹配等）

**使用方式**:
```csharp
var notifyBlock = NotificationBlock.Instance;
notifyBlock.OnChapterLoaded(new ChapterLoadedEventArgs(chapterTitle));
notifyBlock.OnNoMatchTag(new LineNoMatchEventArgs(line, tag));
```

---

### 4️⃣ Utilities/ — 工具类层

#### 4.1 PrtsComponents/ — PRTS 数据处理

##### PrtsPreloader.cs

**职责**: 预加载 PRTS 数据，解析明日方舟标签语法

**核心流程**:
```csharp
public void ParseAndCollectAssets()
{
    foreach (var entry in _textList)
    {
        ParseOriginalText(entry);  // 解析每一行
    }
}

public void ParseOriginalText(FormattedTextEntry entry)
{
    // 1. 使用 UniversalTagsRegex 匹配标签
    // 2. 提取 Tag、Commands、CharName、Dialog
    // 3. 更新当前状态（_currentPortraits, _currentBg 等）
    // 4. 填充 entry 的各字段
}
```

**状态管理**:
- `_currentPortraits`: 当前立绘 URL 列表
- `_currentPortraitFocus`: 当前立绘焦点
- `_currentBg`: 当前背景图 URL
- `_slotUrls`: 角色位置 → URL 映射

**支持的标签**:
- `[name="xxx"]` — 角色对话
- `[charslot]` — 立绘切换
- `[background]` — 背景切换
- `[delay]` — 延迟
- `[decision]` — 选项
- `[sticker]` — 表情
- 等等

##### PrtsDataProcessor.cs

**职责**: 处理 PRTS 原始数据，提取资源链接

**分部类结构**:
- `PrtsDataProcessor.cs` — 主逻辑
- `PrtsDataProcessor.DbSync.cs` — 数据库同步
- `PrtsDataProcessor.PortraitLink.cs` — 立绘链接处理

##### PrtsResLoader.cs

**职责**: 加载 PRTS 资源（图片、音频）

#### 4.2 TagProcessingComponents/ — 标签处理

##### TagProcessor.cs

**职责**: 将明日方舟标签转换为 Markdown/HTML 标签

**转换规则**: 定义在 `PlotRules` 单例中

```csharp
public partial class TagProcessor
{
    private string ProcessTag(FormattedTextEntry entry)
    {
        // 1. 提取标签
        // 2. 验证标签有效性
        // 3. 替换标签（如 [name="xxx"] → **xxx**）
        // 4. 提取标签值
        // 5. 构造结果
    }
}
```

##### PlotManager.cs

**职责**: 管理剧情数据，协调各处理器

```csharp
public class PlotManager
{
    public Plot CurrentPlot { get; set; }
    
    // 解析剧情
    public async Task ParseAllDocuments()
    {
        // 1. 调用 TagProcessor 转换标签
        // 2. 调用 PicDescService 生成图片描述
        // 3. 生成 Markdown 和 Typst 格式
    }
}
```

##### MediaHtmlTagGenerator.cs

**职责**: 生成媒体相关的 HTML 标签（图片、音频、视频）

#### 4.3 WorkFlow/ — 工作流编排

##### AkpStoryLoader.cs

**职责**: 从 GitHub 下载剧情数据，管理下载和缓存

**核心流程**:
```csharp
public async Task GetAllChapters(IEnumerable<string> chaptersToLoad)
{
    // 1. 清理历史脏缓存
    // 2. 查询已缓存章节（Status=2）
    // 3. 已缓存 → 从 DB 加载
    // 4. 未缓存 → 从 GitHub 下载
    // 5. 保存到缓存（Status=1）
}
```

**缓存策略**:
- 已缓存章节（Status=2）直接从 DB 加载，不重新下载
- 新下载章节保存为 Status=1，等待解析

##### AkpParser.cs

**职责**: 解析剧情数据，生成最终输出

**核心流程**:
```csharp
public async Task ParseAllDocuments()
{
    foreach (var plotManager in _storyLoader.ContentTable)
    {
        await plotManager.ParseAllDocuments();
    }
}
```

##### StoryDocument/ — 文档构建

###### StoryDocumentBuilder.cs

**职责**: 构建剧情文档，协调分析器和渲染器

**架构**:
```
StoryDocumentBuilder
  ├── PicDescEnricher     # 填充图片描述
  ├── PortraitProcessor   # 处理立绘
  ├── SegmentGrouper      # 分段分组
  └── IMdRenderer         # 渲染器接口
      ├── ReadableRenderer  # 可读模式（HTML table + 散文）
      └── PromptRenderer    # Prompt 模式（HTML aside + YAML）
```

###### OutputMode.cs

**职责**: 定义输出模式枚举

```csharp
public enum OutputMode
{
    Readable,  // 可读模式：HTML table + 散文 → 人类阅读
    Prompt     // Prompt 模式：HTML aside + YAML → LLM 输入
}
```

**两种模式的区别**:
- **Readable 模式**: 立绘组织为 HTML 表格，场景描述为散文，适合人类阅读
- **Prompt 模式**: 立绘内联为 `<aside class="portrait-facts">`，场景描述为 YAML，适合 LLM 输入

#### 4.4 TypstComponents/ — Typst 渲染

##### TypstRenderer.cs

**职责**: 调用 Typst 引擎渲染 PDF

##### TypstTranslator.cs

**职责**: 将 Markdown 转换为 Typst 语法

---

## 🔑 关键设计决策

### 1. 数据库选择：SqlSugar + SQLite

**原因**:
- SQLite 单文件数据库，便于分发和备份
- SqlSugar 提供 CodeFirst 自动建表，减少手写 SQL
- 支持 JSON 字段自动序列化（`CommandSet`、`Portraits` 等）

**遵循规范**: 参考 `sugar-orm-best-practices` 项目的 DESIGN.md

### 2. 图片描述：两阶段提取

**阶段一**: 视觉模型生成散文描述  
**阶段二**: LLM 从散文提取 YAML 结构化事实

**原因**:
- 散文描述适合人类阅读
- YAML 结构化事实适合 LLM 输入，避免照抄散文
- 两阶段在 `GetOrCreatePicDescWithFactsAsync` 内部完成，确保原子性

### 3. 输出模式：Readable vs Prompt

**原因**:
- 人类阅读需要美观的排版（表格、散文）
- LLM 输入需要结构化数据（YAML、内联标签）
- 两种模式共享同一份数据管线，仅在渲染阶段产生差异

### 4. 缓存策略：Status 字段

**原因**:
- 区分"已下载"和"已解析"两个状态
- 支持断点续传（下载失败后可重试）
- 避免重复下载和解析

---

## 🔗 依赖关系

### 内部依赖

```
ArkPlot.Core
  └── ArkPlot.Vision  # 视觉模型调用（Ollama/百炼）
```

### 外部依赖（关键）

| 包名 | 用途 |
|------|------|
| SqlSugarCore | ORM 框架 |
| Markdig | Markdown 解析 |
| CommunityToolkit.Mvvm | MVVM 基础设施 |
| Microsoft.Data.Sqlite | SQLite 提供程序 |

---

## 🚨 常见陷阱

### 1. FormattedTextEntry 的深拷贝

**问题**: 直接赋值会导致引用共享，修改一个会影响另一个  
**解决**: 使用复制构造函数

```csharp
var copy = new FormattedTextEntry(original);
```

### 2. PicDescService 的缓存键

**问题**: 立绘的 DedupKey 必须是纯 CharacterCode，不能带回 imageUrl  
**解决**: 传入 characterCode 时，自动去除 `#` 后缀

```csharp
var dedupKey = characterCode ?? imageUrl;
if (characterCode != null)
{
    var hashIdx = dedupKey.IndexOf('#');
    if (hashIdx >= 0) dedupKey = dedupKey[..hashIdx];
}
```

### 3. 标签解析的状态管理

**问题**: `PrtsPreloader` 维护当前状态（`_currentPortraits`、`_currentBg`），解析顺序很重要  
**解决**: 按 `Index` 顺序遍历 `_textList`，不要并行处理

### 4. 数据库连接字符串

**问题**: 测试和生产使用不同的连接字符串  
**解决**: 使用 `DbFactory.ConfigureForTesting()` 切换

```csharp
// 测试
DbFactory.ConfigureForTesting("Data Source=:memory:");

// 生产
DbFactory.Reset();
```

---

## 📝 开发指南

### 添加新的数据模型

1. 在 `Model/` 创建 POCO 类，添加 `[SugarTable]` 特性
2. 在 `DbFactory.GetClient()` 的 `InitTables()` 中注册
3. 运行应用，自动建表

### 添加新的标签类型

1. 在 `PlotRules` 中注册标签映射
2. 在 `TagProcessor` 中添加处理逻辑
3. 编写单元测试验证

### 添加新的输出模式

1. 在 `OutputMode` 枚举中添加新模式
2. 实现 `IMdRenderer` 接口
3. 在 `StoryDocumentBuilder` 中注册渲染器

---

## 🔍 调试技巧

### 查看数据库内容

```csharp
var db = DbFactory.GetClient();
var entries = db.Queryable<FormattedTextEntry>()
    .Where(e => e.PlotId == 123)
    .ToList();
```

### 查看图片描述缓存

```csharp
var picDescs = db.Queryable<PicDescription>()
    .Where(p => p.Source == "Vision")
    .ToList();
```

### 清理缓存

```csharp
// 清理图片描述缓存
db.Deleteable<PicDescription>().ExecuteCommand();

// 清理剧情缓存
await PlotCache.CleanupEmptyPlotsAsync(actId);
```

---

## 📚 相关文档

- `sugar-orm-best-practices/DESIGN.md` — SqlSugar ORM 最佳实践
- `docs/design/plot-cache-optimization.md` — PlotCache 优化设计
- `ArkPlot.Tts/Alignment/` — 对齐算法（NovelAligner）
- `ArkPlot.Novelizer/` — 小说化管线（MdReconstructor）

---

## 🎯 总结

ArkPlot.Core 是一个**领域特定的业务逻辑层**，专注于明日方舟剧情数据的获取、解析和转换。它的核心价值在于：

1. **封装复杂性**: 将明日方舟的标签语法、资源管理、多格式输出等复杂性封装在内部
2. **提供统一接口**: 对外提供简单的 API（如 `GetOrCreatePicDescAsync`、`ParseAllDocuments`）
3. **支持多模式**: 同时支持人类可读模式和 LLM Prompt 模式
4. **高效缓存**: 通过数据库缓存避免重复下载和解析

**下一步行动**:
- 如需添加新游戏支持，参考 `IGameScriptParser` 接口设计
- 如需优化性能，关注 `PicDescService` 的并发调用和 `PlotCache` 的批量操作
- 如需调试问题，使用 `NotificationBlock` 监听事件，查看 `Data/PicCache/` 目录

---

**愿这份遗嘱帮助你快速上手，避免踩坑。** 🙏
