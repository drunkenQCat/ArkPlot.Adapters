---
name: refactor-namespace-file-organization
description: 安全地将 .NET 源文件移动到子目录并同时更新命名空间，避免编码损坏和编译错误
source: auto-skill
extracted_at: '2026-06-29T08:35:13.345Z'
---

# .NET 文件重组 + 命名空间重命名指南

## 适用场景

需要将平铺的 `.cs` 文件按职责组织到子目录中（如 `Adapters/`、`Parsing/`、`Data/`），并同步更新命名空间。

## 步骤

### 1. 创建子目录 + 移动文件

使用 shell `move` 命令（Windows）或 `git mv`：

```bash
cd path/to/project
mkdir Adapters Parsing Data
move MyFile.cs Adapters\
move MyOtherFile.cs Parsing\
```

使用 `git mv` 可保留 git 历史追踪：

```bash
git mv MyFile.cs Adapters/
```

### 2. 更新命名空间

**⚠️ 关键风险**：PowerShell 的 `Set-Content` 默认使用 UTF-16 LE 编码，会损坏含中文的文件。必须使用 `[IO.File]::ReadAllText` + `WriteAllText` 指定 UTF-8。

```powershell
# ✅ 安全的命名空间替换（UTF-8 编码）
$utf8 = New-Object System.Text.UTF8Encoding($false)
Get-ChildItem "path\to\Project\Adapters\*.cs" | ForEach-Object {
    $c = [IO.File]::ReadAllText($_.FullName, $utf8)
    $c = $c.Replace('namespace OldNamespace;', 'namespace NewNamespace;')
    [IO.File]::WriteAllText($_.FullName, $c, $utf8)
}
```

```powershell
# ❌ 危险！Set-Content 会损坏中文文件
(Get-Content $_.FullName -Raw) -replace 'old', 'new' | Set-Content $_.FullName
```

### 3. 批量添加跨目录 using 语句

子目录分配好命名空间后，添加 `using` 引用：

```powershell
$utf8 = New-Object System.Text.UTF8Encoding($false)
$add = @{
    'Adapters' = @('using ArkPlot.Arknights.Data;', 'using ArkPlot.Arknights.Parsing;')
    'Parsing'  = @('using ArkPlot.Arknights.Data;')
    'Data'     = @('using ArkPlot.Arknights.Parsing;')
}
foreach ($d in $add.Keys) {
    Get-ChildItem "Project\$d\*.cs" | ForEach-Object {
        $c = [IO.File]::ReadAllText($_.FullName, $utf8)
        $usings = $add[$d]
        $insert = ($usings | Where-Object { $c -notmatch [regex]::Escape($_) }) -join "`n"
        if ($insert) {
            $c = $c -replace '(namespace )', "$insert`n`$1"
            [IO.File]::WriteAllText($_.FullName, $c, $utf8)
        }
    }
}
```

### 4. 构建验证

```bash
dotnet build path/to/Project.csproj 2>&1 | findstr "error CS"
```

所有 `CS0246`（类型未找到）都是缺少 `using` 声明的，重复步骤 3 即可。

### 5. 更新测试项目中的 using

如果测试引用了移动后的类型（如 `ArknightsScriptParser` 移到了 `Adapters` 命名空间），添加：

```powershell
$utf8 = New-Object System.Text.UTF8Encoding($false)
$u = 'using NewNamespace;'
Get-ChildItem "Tests\*.cs" | ForEach-Object {
    $c = [IO.File]::ReadAllText($_.FullName, $utf8)
    if ($c -match 'TypeName' -and $c -notmatch [regex]::Escape($u)) {
        $c = $c -replace '(namespace )', "$u`n`$1"
        [IO.File]::WriteAllText($_.FullName, $c, $utf8)
    }
}
```

### 6. 最终验证

```bash
dotnet build path/to/Project.csproj
dotnet test path/to/Tests.csproj
```

## 注意事项

- **编码是最大的陷阱**：含有中文/日文/特殊字符的 `.cs` 文件必须用 `[IO.File]::ReadAllText/WriteAllText` + UTF8 编码
- **partial class** 的多个文件必须在**同一个命名空间**下，否则编译失败
- `git mv` 优于 `move` 可保留 git history
- 运行 `dotnet format` 结尾可自动修正缩进等格式问题