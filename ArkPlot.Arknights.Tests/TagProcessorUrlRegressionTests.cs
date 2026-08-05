using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.TagProcessing;
using ArkPlot.Arknights.Workflow;
using ArkPlot.Core.Infrastructure;
using ArkPlot.Core.Model;

namespace ArkPlot.Arknights.Tests;

[Collection("DbTests")]
public class TagProcessorUrlRegressionTests : IDisposable
{
    public TagProcessorUrlRegressionTests()
    {
        DbFactory.ConfigureForTesting("Data Source=:memory:");
        ArknightsDbInitializer.Init(DbFactory.GetClient());
    }

    public void Dispose()
    {
        DbFactory.Reset();
        ArknightsDbInitializer.Reset();
    }

    private static void InsertResource(string type, string key, string url)
    {
        var db = DbFactory.GetClient();
        db.Insertable(new PrtsResource
        {
            ResourceType = type,
            ResourceKey = key,
            ResourceUrl = url
        }).ExecuteCommand();
    }

    private static string TagsJsonPath()
    {
        // 测试项目引用了 ArkPlot.Avalonia 时，tags.json 会复制到输出目录；
        // 否则尝试从 Avalonia 项目目录读取。
        var testDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(testDir, "tags.json");
        if (File.Exists(candidate)) return candidate;

        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        candidate = Path.Combine(repoRoot, "ArkPlot.Avalonia", "tags.json");
        if (File.Exists(candidate)) return candidate;

        throw new FileNotFoundException("找不到 tags.json 测试规则文件", candidate);
    }

    [Fact]
    public void ProcessEntry_Background_UsesDbUrl()
    {
        const string expectedUrl = "https://example.com/bg_white.png";
        InsertResource("Image", "bg_white", expectedUrl);

        var parser = new AkpParser(TagsJsonPath());
        var entry = new FormattedTextEntry
        {
            OriginalText = "[background(image=\"white\")]",
            Type = "background",
            ResourceUrls = [expectedUrl]
        };

        var result = parser.ProcessSingleLine(entry);

        Assert.Contains($"src=\"{expectedUrl}\"", result);
    }

    [Fact]
    public void ProcessEntry_Portrait_UsesDbUrl()
    {
        const string expectedUrl = "https://example.com/avg_char_123.png";
        InsertResource("Char", "avg_char_123", expectedUrl);

        var parser = new AkpParser(TagsJsonPath());
        var entry = new FormattedTextEntry
        {
            OriginalText = "[character(name=\"avg_char_123\")]",
            Type = "character",
            ResourceUrls = [expectedUrl]
        };

        var result = parser.ProcessSingleLine(entry);

        Assert.Contains($"src=\"{expectedUrl}\"", result);
    }

    [Fact]
    public void ProcessEntry_Audio_UsesDbUrl()
    {
        const string expectedUrl = "https://example.com/d_avg_clothmovement.mp3";
        InsertResource("Audio", "d_avg_clothmovement", expectedUrl);

        var parser = new AkpParser(TagsJsonPath());
        var entry = new FormattedTextEntry
        {
            OriginalText = "[playsound(key=\"$d_avg_clothmovement\")]",
            Type = "playsound",
            ResourceUrls = [expectedUrl]
        };

        var result = parser.ProcessSingleLine(entry);

        Assert.Contains($"src=\"{expectedUrl}\"", result);
    }

    [Fact]
    public void ProcessEntry_GridBg_SplitsImageGroupIntoMultipleUrls()
    {
        const string url1 = "https://example.com/bg_a.png";
        const string url2 = "https://example.com/bg_b.png";
        InsertResource("Image", "bg_a", url1);
        InsertResource("Image", "bg_b", url2);

        var parser = new AkpParser(TagsJsonPath());
        var entry = new FormattedTextEntry
        {
            OriginalText = "[gridbg(imagegroup=\"a/b\")]",
            Type = "gridbg",
            ResourceUrls = [url1, url2]
        };

        var result = parser.ProcessSingleLine(entry);

        Assert.Contains($"src=\"{url1}\"", result);
        Assert.Contains($"src=\"{url2}\"", result);
    }
}
