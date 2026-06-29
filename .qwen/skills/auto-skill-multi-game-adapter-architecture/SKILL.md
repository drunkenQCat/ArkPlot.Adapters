---
name: multi-game-adapter-architecture
description: 将现有单游戏剧情解析引擎重构为通用管线 + 游戏适配器插件架构，支持多游戏适配
source: auto-skill
extracted_at: '2026-06-29T06:48:38.713Z'
---

# 多游戏适配器架构重构指南

## 适用场景

你有一个**深度耦合特定游戏逻辑**的剧情解析项目（如 ArkPlots），目标是将它重构为：
- 一个**通用剧情解析引擎**（不依赖任何特定游戏）
- 通过**插件式的游戏适配器**来支持不同游戏

## 核心思路

将管线中的每步操作抽象为接口，每种游戏实现一套适配器，由通用管线编排调用。

```
原始管线的耦合分布：
数据获取 🟥 → 脚本解析 🟥 → 资源关联 🟥 → 图片描述 🟩 → 文档渲染 🟩
(🟥=游戏特有, 🟩=通用)

目标架构：
                   ┌──────────────┐
                   │  StoryPipeline │ ← 通用编排
                   └──────┬───────┘
            ┌─────────────┼─────────────┐
            ▼             ▼             ▼
     ┌──────────┐  ┌──────────┐  ┌──────────┐
     │Arknights │  │  Game B  │  │  Game C  │
     │Adapter   │  │  Adapter │  │  Adapter │
     └──────────┘  └──────────┘  └──────────┘
```

## 第一步：定义 4 个核心接口

### IStoryDataProvider — 数据获取

```csharp
public interface IStoryDataProvider
{
    IReadOnlyList<string> SupportedLanguages { get; }
    Task SyncMetadataAsync(string lang);
    Task<string?> FetchChapterAsync(StoryChapter chapter, CancellationToken ct);
    Task<string?> GetLatestVersionAsync(string lang);
    Dictionary<string, (string Url, long ChapterId)> GetChapterUrls(
        List<StoryChapter> chapters, string lang);
}
```

职责：从远程源（GitHub 仓库、API 等）下载剧情数据。

### IScriptParser — 脚本解析

```csharp
public interface IScriptParser
{
    string GameId { get; }
    List<ScriptLine> Parse(string rawText, string chapterTitle);
    HashSet<ResourceRef> CollectResources(List<ScriptLine> lines);
}
```

职责：将原始脚本文本（含游戏特有标签语法）解析为通用 `ScriptLine` 列表。
解析是有状态的：当前背景、当前立绘等按行顺序累积传播。

### IResourceResolver — 资源解析

```csharp
public interface IResourceResolver
{
    string? NormalizeCharacterCode(string rawName);
    string ResolvePortraitUrl(string characterCode, string? variant = null);
    string ResolveBackgroundUrl(string bgKey);
    string ResolveAudioUrl(string audioKey);
}
```

职责：将游戏内资源标识符转换为可下载的 URL。

### ITagRenderer — 标签渲染

```csharp
public interface ITagRenderer
{
    string RenderLine(ScriptLine line);
    bool RequiresRulesFile { get; }
    void LoadRules(string rulesFilePath);
}
```

职责：将 ScriptLine 中的游戏特有标签转换为 Markdown 文本。

## 第二步：提取通用模型基类

将现有的深度耦合模型拆分为"通用基类 + 游戏特有子类"：

```csharp
// 通用基类（放在 Core 层）
public class ScriptLine
{
    public int Index { get; set; }
    public string OriginalText { get; set; } = "";
    public string MdText { get; set; } = "";
    public string Type { get; set; } = "";
    public StringDict CommandSet { get; set; } = new();
    public string CharacterName { get; set; } = "";
    public string? CharacterCode { get; set; }
    public string Dialog { get; set; } = "";
    public List<string> ResourceUrls { get; set; } = new();
    public string PicDesc { get; set; } = "";
    [SugarColumn(IsIgnore = true)]
    public string PicFacts { get; set; } = "";
}
```

> **SqlSugar 继承陷阱**：基类字段没有 `[SugarColumn]` 时，CodeFirst 不会自动建表。
> 必须在子类中 **`new` + 附加 `[SugarColumn]`**：
> ```csharp
> [SugarTable("FormattedTextEntry")]
> public class FormattedTextEntry : ScriptLine
> {
>     [SugarColumn(Length = 1000)]
>     public new string OriginalText { get => base.OriginalText; set => base.OriginalText = value; }
>
>     [SugarColumn(IsJson = true, ColumnDataType = "TEXT")]
>     public new StringDict CommandSet { get => base.CommandSet; set => base.CommandSet = value; }
>
>     // 子类特有字段
>     public int PortraitFocus { get; set; }
>     public bool SkipPortraitOutput { get; set; }
> }
> ```

通用字段的判断标准：**任何游戏都可能用到**的字段放入基类。
- 确定通用：`Index`, `OriginalText`, `Type`, `CharacterName`, `Dialog`, `ResourceUrls`
- 可能通用：`Bg`（背景）, `Portraits`（角色图）
- 特有：`PortraitFocus`（方舟立绘焦点布局）, `SkipPortraitOutput`

## 第三步：创建通用管线编排

```csharp
public class StoryPipeline
{
    private readonly IStoryDataProvider _provider;
    private readonly IScriptParser _parser;
    private readonly ITagRenderer _renderer;
    private readonly PicDescService _picDescService;

    public async Task<List<ScriptLine>> ProcessChapterAsync(
        StoryChapter chapter, string lang, CancellationToken ct)
    {
        var rawText = await _provider.FetchChapterAsync(chapter, ct);
        var lines = _parser.Parse(rawText, chapter.StoryName);
        RenderLines(lines);
        await EnrichDescriptionsAsync(lines, ct);
        return lines;
    }

    public string BuildDocument(List<ScriptLine> lines, OutputMode mode) { ... }
}
```

管线不引用任何游戏特有类。所有游戏特性通过接口注入。

## 第四步：分阶段迁移

### 阶段 1 — 定义接口 + 模型拆分（不改行为）

**只新建，不移动**：
1. 在 Core 中定义 4 个接口 + `ScriptLine` 基类
2. 现有子类继承基类（`new` + `[SugarColumn]` 技巧）
3. 创建 `StoryPipeline` 编排类
4. 分析原管线中大文件（653 行等），标记拆分点但暂不动

**不动**：现有代码不改行为，不移动文件。

**验证**：全部现有测试通过。

### 阶段 2 — 创建适配器项目（适配器模式包装）

关键实践：**不立即移动源文件**，先创建适配器类包装现有实现。

1. 新建 `ArkPlot.{GameName}` 项目，引用 Core
2. 添加 `InternalsVisibleTo` 到 Core 项目
3. 给 Core 中需暴露的内部方法加 `internal` 入口（如 `TagProcessor.ProcessEntry`）
4. 实现 4 个接口，内嵌现有类的实例

**文件映射示例**：

| 现有类（Core） | 适配器（Arknights） |
|----------------|--------------------|
| PrtsPreloader  | ArknightsScriptParser |
| PrtsDataProcessor + PrtsAssets | ArknightsResourceResolver |
| TagProcessor + PlotRules | ArknightsTagRenderer |
| StorySyncService 下载逻辑 | ArknightsStoryProvider |

```csharp
// 适配器模式示例：TagProcessor 需要新暴露 internal 方法
// 在 Core/TagProcessor.cs 添加：
internal string ProcessEntry(FormattedTextEntry entry) => ProcessTag(entry);

// 适配器类：
public class ArknightsTagRenderer : ITagRenderer
{
    private readonly TagProcessor _processor = new();

    public string RenderLine(ScriptLine line)
    {
        if (line is not FormattedTextEntry entry) return line.OriginalText;
        return _processor.ProcessEntry(entry);
    }
}
```

### 阶段 3 — 清理 Core 中的游戏残留

核心原则：**小文件、小函数**。原文件 600+ 行时必须主动拆分。

#### 3.1 消除全局单例（关键步骤）

全局单例是测试隔离和跨游戏并行最大的障碍。消除步骤：

1. **给目标类移除 `static Instance` 属性**，使构造函数变成 `public`
2. **给消费者类添加构造函数参数**，用于接收实例
3. **保留无参构造函数 `new()` 作为默认**，兼容现有调用方
4. **更新所有 `ClassName.Instance` 调用点为实例字段/局部变量**
5. 对于静态工具类（如 `NetworkUtility`），在各方法内 `new()` 临时实例

```csharp
// Before: 单例
public class TagProcessor
{
    public readonly PlotRules Rules = PlotRules.Instance;
    public TagProcessor() { }
}

// After: 构造函数注入（保留默认值兼容）
public class TagProcessor
{
    public readonly PlotRules Rules;
    private readonly NotificationBlock _notify;

    public TagProcessor() : this(new PlotRules(), new NotificationBlock()) { }
    public TagProcessor(PlotRules rules, NotificationBlock? notify = null)
    {
        Rules = rules;
        _notify = notify ?? new NotificationBlock();
    }
}
```

典型需要消除的单例及其影响范围：

| 单例 | 使用点数量 | 消除方式 |
|------|-----------|---------|
| `PrtsAssets.Instance` | ~8 处 | 构造函数注入 PrtsAssets 实例 |
| `PlotRules.Instance` | ~3 处 | 构造函数注入 PlotRules |
| `NotificationBlock.Instance` | ~15 处 | 直接 `new NotificationBlock()` 或构造函数注入 |

#### 3.2 魔术数字 → 枚举

```csharp
// Before
plot.Status = 1;
if (plot.Status == 2) { ... }

// After
public enum PlotStatus { Unprocessed = 0, Downloaded = 1, Parsed = 2 }

public static async Task SaveAsync(Plot plot, ..., PlotStatus status = PlotStatus.Parsed)
{
    plot.Status = (int)status;
}
```

数据库存储的 int 值不变，代码中不再出现裸数字。

#### 其他清理项

| 清理项 | 方案 |
|--------|------|
| `PicDescService.SkipUrls` 硬编码 | 构造函数接收 `additionalSkipUrls` 参数 |
| `DbFactory` 全局单例 | 添加 `IDbFactory` 接口 + `DefaultDbFactory` 实现 |
| `PromptRenderer` 方舟标签名 | 提取 `PromptRendererConfig` 可配置类 |
| `GitHubProxy.Prefix` 全局状态 | 适配器负责管理自己的前缀 |

#### 3.3 大文件拆分（小文件原则）

以 PrtsPreloader (653 行) 拆分为 6 个文件为例：

| 文件 | 职责 | 行数 |
|------|------|------|
| `PrtsPreloader.cs` | 状态字段 + 构造函数 + ParseOriginalText 主流程 | ~170 |
| `PrtsPreloader.CommandDispatch.cs` | ProcessCommand 命令分发 | ~110 |
| `PrtsPreloader.PortraitState.cs` | 立绘/槽位状态跟踪 | ~70 |
| `PrtsPreloader.Processors.cs` | 图片/立绘/大图/音频处理 | ~120 |
| `PrtsPreloader.Overrides.cs` | PRTS 数据覆盖系统 | ~110 |
| `Matched.cs` | 辅助类（单独文件） | ~10 |

拆分要点：
- **Partial class 跨文件**：同一个 `partial class PrtsPreloader` 分布在多个 .cs 文件中，namespace 必须一致
- **按职责而非调用顺序**：不要按从上到下的调用链切文件，按"数据覆盖"、"立绘状态"等业务领域划分
- **状态集中，功能散开**：共享字段（`_counter`, `_currentPortraits`, `_slotUrls` 等）集中在主文件，各 function file 通过 partial class 机制共享状态
- **提取辅助类**：如 `Matched` 这种 POCO 单独文件，不留在主文件末尾

#### 3.4 源文件物理迁移（Adapter → 直接实现）

阶段 2 使用适配器模式包装（Core 中保留源文件），阶段 3 将源文件物理移动到适配器项目。

**迁移步骤：**
1. 复制文件到目标项目，更新 namespace（如 `ArkPlot.Core.Utilities.PrtsComponents` → `ArkPlot.Arknights`）
2. 补充目标项目需要的 `using ArkPlot.Core.Model/Services/Utilities` 导入
3. **处理循环引用**：如果 Core 中的其他文件引用了正在迁移的类（如 `AkpStoryLoader` 引用 `PrtsPreloader`），这些文件也必须迁移到适配器项目，否则 Core 会产生对适配器项目的循环引用
4. **处理 Linked Files**：如果源文件是通过 `<Compile Include="..\OtherProject\Model\**\*.cs">` 链接的，用 `<Exclude>` 排除已迁移的文件：
   ```xml
   <Compile Include="..\OtherProject\Model\**\*.cs"
            Exclude="..\OtherProject\Model\PlotRules.cs;..\OtherProject\Model\PrtsAssets.cs" />
   ```
5. 删除原文件，清理空目录

**常见错误与修复：**

| 错误 | 原因 | 修复 |
|------|------|------|
| `CS0234: namespace ... not exist` | 旧 `using` 指向已删除的 namespace | 删除旧的 `using`，类型已在同 namespace |
| `CS0103: name not exist` | 缺少 `using ArkPlot.Core.Utilities` | 补充对应 using |
| `CS0246: Type 'X' not found` | 缺少 `using System.Text` 等 | 补充标准库 using |
| 编译通过但运行时类型不匹配 | 源文件仍在两个项目中同时编译 | 用 `<Exclude>` 排除，确保只编译一次 |

## 第三步彩蛋：文档渲染器不需 tag.json

**经验总结**：`tag.json` 是方舟文本标签（`[name="xxx"]`）的妥协方案。如果游戏数据已经是结构化的（JSON/YAML），不需要正则查表，只需要模板拼接。

```csharp
/// 简单标签渲染器：面向结构化数据的游戏，不需要 tag.json。
public class SimpleTagRenderer : ITagRenderer
{
    public bool RequiresRulesFile => false;

    /// 对话行模板。默认：**角色名** 对话内容
    public string DialogTemplate { get; set; } = "**{speaker}** {text}";
    /// 旁白行模板。默认：原文
    public string NarrationTemplate { get; set; } = "{text}";

    public void LoadRules(string rulesFilePath) { } // 空实现

    public string RenderLine(ScriptLine line) { ... }
}
```

这也意味着 `ITagRenderer` 的 `RenderLine` 方法应该**足够通用**，适配器可以自由选择查 JSON 规则文件、模板拼接、甚至不做任何转换。

## 第三步入门：Type 名称映射（隐藏最大的坑）

重构后发现最严重的问题不是状态管理，而是 **Core 层的组件直接引用了游戏特有的 Type 名称**。

### 问题

`StoryDocumentBuilder`, `SegmentGrouper`, `PromptRenderer`, `PortraitProcessor` 多处硬编码方舟类型名：

```csharp
if (entry.Type is "character" or "charslot" or "charactercutin")      // PortraitProcessor
if (entry.Type is "background" or "largebg")                           // PromptRenderer
if (entry.Type is "showitem" or "cgitem" or "interlude" or "image")   // PromptRenderer
if (entry.Type == "playmusic")                                          // SegmentGrouper
```

这些都在 Core 层，但它们全是方舟逻辑。

### 解决方案：集中式类型映射（B 方案）

不要在解析时零散映射（A 方案，**不推荐**），而是在 **管线入口集中映射**：

```
原始类型名（方舟: "charslot", 假设AVG: "char_show"）
       ↓
    管线入口：TypeMap(rawType) → 通用类型名
       ↓
    下游组件统一看通用名（"portrait", "dialog", "background", "audio", "narration"）
```

**谁来做**：每个适配器提供一个映射表，`StoryPipeline` 在 `Parse()` 输出后统一应用：

```csharp
public interface IScriptParser
{
    IReadOnlyDictionary<string, string> TypeMappings { get; }  // rawType → genericType
}
```

**定义一套通用类型集**供所有适配器对齐：

| 通用类型 | 对应语义 |
|---------|---------|
| `dialog` | 有说话人的对话 |
| `narration` | 无说话人的旁白 |
| `portrait` | 立绘/角色显示 |
| `background` | 背景切换 |
| `audio` | 音乐/音效 |
| `effect` | 特效/滤镜 |
| `separator` | 分段标记 |

Type 映射应在 `SegmentGrouper`、`PortraitProcessor` 等组件处理**之前**完成。

### 验证新游戏

不要只在代码里想。**动真格写一个假假游戏适配器**来验证接口完备性。用完全不同的数据格式（JSON 代替标签文本），跑同一套 StoryPipeline。

```csharp
// 假假游戏端到端验证
var pipeline = new StoryPipeline(
    provider: new OfflineFakeGameProvider(json),
    parser: new FakeGameScriptParser(),
    renderer: new FakeGameTagRenderer(),
    picDescService: new PicDescService());

var results = await pipeline.ProcessEventAsync(act, chapters, OutputMode.Readable);
Assert.Contains("Alice", results[0].Markdown);
```

### 验证清单（从一个真实项目中总结）

| 验证项目 | 方舟 | 假假游戏 |
|---------|------|---------|
| 数据格式 | 行式标签文本 `[name=""]` | 结构化 JSON |
| 需要 tag.json | ✅ 需要 | ❌ 不需要 |
| 立绘管理 | 3 槽位 + 显式 focus | 2 槽位 + 自动 focus |
| 状态模型 | 累积（当前背景/立绘） | 有限自包含 + 背景累积 |
| Type 映射 | 适配器内做 | 适配器内做 |
| `TypeMappings` 应用 | 管线入口统一应用 | 管线入口统一应用 |
| DB 支撑 | 支持（SqlSugar + Prefix DB） | 理论上支持 |

## 关键设计决策

| 决策 | 建议 |
|------|------|
| 适配器项目位置 | 放在解决方案内，与 Core 同级 |
| 模型拆分层级 | 第一阶段用继承（ScriptLine ← FormattedTextEntry），留意 SqlSugar `new` 技巧 |
| 大文件拆分 | 遵循"代码简洁之道"：小函数、小文件，先标记拆分点、阶段 3-4 再物理拆分 |
| 缓存/DB | 保留在 Core 层，是通用关注点 |
| 图片描述服务 | 保留在 Core 层，视觉模型不依赖游戏 |
| 文档渲染器 | 保留在 Core 层，渲染逻辑通用；游戏特有标签名通过 PromptRendererConfig 注入 |
| 游戏特有占位图 | 通过 PicDescService 构造函数的 `additionalSkipUrls` 参数配置 |
| 源文件位置 | 阶段 2 做适配器包装，阶段 3 才物理迁移源文件（或保持 Compile Link） |

## 验证策略

- 每阶段都有完整的测试回归（原测试全绿 + 新适配器测试全绿）
- 阶段 2 使用真实游戏数据（如 prefix DB）做端到端验证
- 阶段 4 用 mock 游戏验证接口完备性
- 优先编写纯逻辑测试（无 DB/网络依赖），它们最快最稳定，是重构时的最高安全保障