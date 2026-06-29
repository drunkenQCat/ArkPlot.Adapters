using System.IO;

using ArkPlot.Arknights.Data;
using ArkPlot.Arknights.Parsing;
using ArkPlot.Arknights.TagProcessing;
namespace ArkPlot.Arknights.Workflow;

public abstract partial class AkpProcessor
{
    public static void WriteTyp(string outputPath, AkpStoryLoader contentLoader)
    {
        var plotList = contentLoader.ContentTable;
        var templateFolder = Path.Join(Directory.GetCurrentDirectory(), "typst-template");
        CopyDirectory(templateFolder, outputPath);

        int fileIndex = 1;
        foreach (var plot in plotList)
        {
            var header = "#import \"./template.typ\": arknights_sim, arknights_sim_2p\n";
            var content = string.Join("\n", plot.CurrentPlot.TextVariants.Select(x => x.TypText));
            var result = header + content;
            var currentTyp = Path.Join(outputPath, $"{fileIndex}_{plot.CurrentPlot.Title}.typ");
            File.WriteAllText(currentTyp, result);
            fileIndex++;
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), true);
    }
}
