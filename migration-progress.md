# ArkPlot.Core → ArkPlot.Adapters 迁移进度报告

> 生成时间：2026-07-02
> 检查方式：全量 `dotnet build` + csproj 引用分析 + 代码 grep

## 一、总览

| 检查项 | 状态 | 说明 |
|---|---|---|
| sln 项目路径 | ✅ 已迁移 | sln 中 `ArkPlot.Core` 指向 `ArkPlot.Adapters\ArkPlot.Core\` |
| csproj 引用（10 个项目） | ✅ 已迁移 | 所有项目 csproj 指向 Adapters Core |
| Adapters 子模块代码 | ✅ 已完成 | Core/Arknights/FakeGame 代码已就位 |
| ArkPlot.Cli 代码适配 | ⚠️ 6 个编译错误 | using 部分更新，残留旧命名空间引用 |
| ArkPlot.Avalonia 代码适配 | ⚠️ 1 个编译错误 | 残留旧命名空间引用 |
| ArkPlotWpf 代码适配 | ✅ 无错误 | 仅有 nullable warning |
| 旧 `ArkPlot.Core\` 文件夹 | ❌ 未删除 | 68 个 .cs 文件仍在磁盘上（不在 sln 中） |
| 旧 `ArkPlot.Core.Tests\` 文件夹 | ❌ 未删除 | 残留 csproj + 1 个测试文件（不在 sln 中） |

**编译结果**：7 个 CS 错误（3 个项目），0 个迁移相关错误的其他项目。

---

## 二、编译错误明细

### 2.1 命名空间未更新（5 处，可自动修复）

| # | 文件 | 行号 | 错误码 | 问题 | 修复方案 |
|---|---|---|---|---|---|
| 1 | `ArkPlot.Cli/Pipeline/CliPipeline.cs` | 53 | CS0234 | `ArkPlot.Core.Utilities.WorkFlow.AkpParser` 不存在 | 改为 `ArkPlot.Arknights.Workflow.AkpParser` |
| 2 | `ArkPlot.Cli/Pipeline/DocumentParser.cs` | 16 | CS0246 | `AkpParser` 找不到 | 添加 `using ArkPlot.Arknights.Workflow;` |
| 3 | `ArkPlot.Cli/Pipeline/ResourceLoader.cs` | 19 | CS0246 | `PrtsDataProcessor` 找不到 | 添加 `using ArkPlot.Arknights.Data;` |
| 4 | `ArkPlot.Cli/Pipeline/DiagnoseAlignmentBatchRunner.cs` | 62 | CS0234 | `ArkPlot.Core.Model.FormattedTextEntry` 不存在 | 改为 `ArkPlot.Arknights.FormattedTextEntry` |
| 5 | `ArkPlot.Avalonia/ViewModels/MainWindowViewModel.cs` | 43 | CS0246 | `PrtsDataProcessor` 找不到 | 添加 `using ArkPlot.Arknights.Data;` |

#### 旧→新命名空间映射表

```
ArkPlot.Core.Model.FormattedTextEntry
  → ArkPlot.Arknights.FormattedTextEntry

ArkPlot.Core.Utilities.WorkFlow.AkpParser
  → ArkPlot.Arknights.Workflow.AkpParser

ArkPlot.Core.Utilities.WorkFlow.AkpStoryLoader
  → ArkPlot.Arknights.Workflow.AkpStoryLoader

ArkPlot.Core.Utilities.PrtsComponents.PrtsDataProcessor
  → ArkPlot.Arknights.Data.PrtsDataProcessor

ArkPlot.Core.Utilities.PrtsComponents.PrtsPreloader
  → ArkPlot.Arknights.Parsing.PrtsPreloader

ArkPlot.Core.Utilities.TagProcessingComponents.PlotManager
  → ArkPlot.Arknights.TagProcessing.PlotManager

ArkPlot.Core.Utilities.TagProcessingComponents.TagProcessor
  → ArkPlot.Arknights.TagProcessing.TagProcessor

ArkPlot.Core.Utilities.AkpProcess
  → ArkPlot.Arknights.Workflow.AkpProcessor

ArkPlot.Core.Data.ArkPlotRegs
  → ArkPlot.Arknights.Parsing.ArkPlotRegs

ArkPlot.Core.Model.PrtsData        → ArkPlot.Arknights.Data.PrtsData
ArkPlot.Core.Model.PrtsAssets      → ArkPlot.Arknights.Data.PrtsAssets
ArkPlot.Core.Model.PrtsResource    → ArkPlot.Arknights.Data.PrtsResource
ArkPlot.Core.Model.PrtsPortraitLink → ArkPlot.Arknights.Data.PrtsPortraitLink
ArkPlot.Core.Model.SentenceMethod  → ArkPlot.Arknights.Data.SentenceMethod
ArkPlot.Core.Model.PlotRules       → ArkPlot.Arknights.TagProcessing.PlotRules
ArkPlot.Core.Model.Alias           → （确认是否仍需要）
```

### 2.2 类型转换错误（2 处，需手动分析）

| # | 文件 | 行号 | 错误码 | 问题 |
|---|---|---|---|---|
| 6 | `ArkPlot.Cli/Pipeline/CliPipeline.cs` | 62 | CS1503 | `List<ScriptLine>` 无法转换为 `List<FormattedTextEntry>` |
| 7 | `ArkPlot.Cli/Pipeline/CliPipeline.cs` | 96 | CS1503 | 同上 |

**根因**：Adapters Core 的 `Plot.TextVariants` 类型从 `List<FormattedTextEntry>` 改为 `List<ScriptLine>`。调用方把 `TextVariants` 传给期望 `List<FormattedTextEntry>` 的方法时，C# 不支持协变转换。

**修复方案**（二选一）：
- A. 在调用处加 `.Cast<FormattedTextEntry>().ToList()` — 最小改动
- B. 把被调方法的参数改为 `List<ScriptLine>` 或 `IList<ScriptLine>` — 更通用但影响面大

---

## 三、旧文件夹残留

### 3.1 `ArkPlot.Core/`（68 个 .cs 文件）

- **不在 sln 中** — 不参与编译
- **仍被代码间接依赖** — grep 显示大量 `FormattedTextEntry` 引用指向旧命名空间（但已通过 Arknights 适配器的 `FormattedTextEntry : ScriptLine` 继承解决）
- **删除前提**：先修完所有编译错误，再确认全量 build 通过后删除

### 3.2 `ArkPlot.Core.Tests/`（1 个 .cs 文件 + csproj）

- **不在 sln 中** — 已被 `ArkPlot.Adapters/ArkPlot.Core.Tests/` 替代
- **csproj 引用**：已指向 Adapters Core ✅，但还引用了 Adapters Arknights
- **测试文件**：仅 `GitHubProxyTests.cs`（已迁移到 Adapters Core.Tests 中？需确认）
- **删除前提**：确认测试已迁移到 Adapters Core.Tests 后删除

---

## 四、已完成的工作清单

1. ✅ Adapters 子模块建立（Core + Arknights + FakeGame + 各 Tests）
2. ✅ Core 接口抽象（8 个 Interfaces）
3. ✅ Arknights 代码从 Core 剥离到独立项目
4. ✅ `ScriptLine` 通用模型替代 `FormattedTextEntry`（FormattedTextEntry 改为继承 ScriptLine）
5. ✅ `StoryPipeline` 通用管线
6. ✅ `StoryDocumentBuilder/Context` 改用 `ScriptLine`
7. ✅ `PlotCache<T>` 泛型化
8. ✅ sln 项目路径更新
9. ✅ 10 个项目 csproj 引用更新
10. ✅ ArkPlot.Cli 部分使用新命名空间（`using ArkPlot.Arknights.*`）

---

## 五、待完成的工作清单

| 优先级 | 任务 | 涉及文件数 | 自动化难度 |
|---|---|---|---|
| P0 | 修复 5 处命名空间错误（2.1 表） | 4 | 🟢 脚本可自动 |
| P0 | 修复 2 处类型转换错误（2.2 表） | 1 | 🟡 需人工判断 |
| P0 | 全量 build 验证零错误 | - | 🟢 自动 |
| P1 | 删除旧 `ArkPlot.Core/` 文件夹 | - | 🟢 自动（build 通过后） |
| P1 | 删除旧 `ArkPlot.Core.Tests/` 文件夹 | - | 🟢 自动（确认测试已迁移后） |
| P1 | 全量 grep 确认无残留旧命名空间引用 | - | 🟢 脚本可自动 |
| P2 | 确认 `GitHubProxyTests.cs` 已迁移到 Adapters Core.Tests | 1 | 🟢 手动对比 |
| P2 | `_tools/ParseEntry/` 中 `FormattedTextEntry` 引用适配 | 1 | 🟡 需确认 |

---

## 六、自动化修复方案

这种迁移本质是 **大规模机械性查找替换 + 编译验证循环**，非常适合自动化。以下是可行的自动化手段：

### 方案 A：PowerShell 脚本批量替换（推荐，最直接）

编写一个 `.ps1` 脚本，对 `ArkPlot.Cli/` 和 `ArkPlot.Avalonia/` 目录下的所有 `.cs` 文件执行：

```powershell
# 1. 全限定名替换（优先处理，避免误伤）
$replacements = @{
    'ArkPlot.Core.Model.FormattedTextEntry'         = 'ArkPlot.Arknights.FormattedTextEntry'
    'ArkPlot.Core.Utilities.WorkFlow.AkpParser'     = 'ArkPlot.Arknights.Workflow.AkpParser'
    'ArkPlot.Core.Utilities.WorkFlow.AkpStoryLoader' = 'ArkPlot.Arknights.Workflow.AkpStoryLoader'
    'ArkPlot.Core.Utilities.PrtsComponents.PrtsDataProcessor' = 'ArkPlot.Arknights.Data.PrtsDataProcessor'
    'ArkPlot.Core.Utilities.PrtsComponents.PrtsPreloader'     = 'ArkPlot.Arknights.Parsing.PrtsPreloader'
    'ArkPlot.Core.Utilities.TagProcessingComponents.' = 'ArkPlot.Arknights.TagProcessing.'
    'ArkPlot.Core.Utilities.AkpProcess'              = 'ArkPlot.Arknights.Workflow.AkpProcessor'
    'ArkPlot.Core.Data.ArkPlotRegs'                  = 'ArkPlot.Arknights.Parsing.ArkPlotRegs'
}

# 2. 对每个 .cs 文件执行替换
# 3. 对缺少 using 的文件，根据编译错误自动添加
# 4. 对 Cast<FormattedTextEntry> 类型转换，用正则匹配后插入
```

**优点**：精确控制、可 review、可回滚
**适用**：命名空间替换（2.1 表的 5 处）

### 方案 B：Roslyn MCP 批量重构

利用已配置的 sharplens MCP 工具：

1. `roslyn_load_solution` 加载解决方案
2. `roslyn_get_diagnostics` 获取所有 CS0234/CS0246/CS1503 错误
3. 对每个错误用 `roslyn_apply_code_action_by_title` 或 `roslyn_rename_symbol` 自动修复
4. 循环直到零错误

**优点**：语义级精确，不会误替换字符串/注释
**适用**：命名空间重构、添加 using

### 方案 C：编译-修复自循环（Agent 驱动）

```
while (build has errors):
    1. dotnet build → 提取 CS 错误列表
    2. 按错误码分类（CS0246=缺using, CS0234=错命名空间, CS1503=类型不匹配）
    3. 对 CS0246/CS0234：查映射表 → 替换或添加 using
    4. 对 CS1503：插入 .Cast<T>().ToList()
    5. 重新 build
```

**优点**：完全自动化，不遗漏
**适用**：最终收敛阶段

### 方案 D：dotnet format + EditorConfig

在 `.editorconfig` 中配置 `dotnet_diagnostic.CS0246.severity = error`，然后：

```shell
dotnet format --diagnostics CS0246 --severity error
```

**局限**：`dotnet format` 只处理代码风格，不处理命名空间迁移。需要配合方案 A/B。

### 推荐组合

1. **方案 A**（PowerShell 脚本）批量处理命名空间替换 — 处理 2.1 表
2. **手动**处理 2 处 `Cast<FormattedTextEntry>` 类型转换 — 处理 2.2 表
3. **方案 C**（build 循环）验证收敛 — 确保零错误
4. 删除旧文件夹 + 全量 grep 确认无残留
