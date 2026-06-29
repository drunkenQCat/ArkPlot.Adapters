using System.IO;
using System.Net.Http;
using System.Threading;
using ArkPlot.Core.Services;
using PreloadSet = System.Collections.Generic.HashSet<System.Collections.Generic.KeyValuePair<string, string>>;

namespace ArkPlot.Arknights;

public class PrtsResLoader
{
    // 荳玖ｽｽ assets 驥碁擇逧?謇譛?assets縲りｦ∵ｱゆｻ紋ｻｬ謾ｾ蛻?output 譁?莉ｶ螟ｹ荳?
    // 菫晏ｭ倡噪譌ｶ蛟呵ｦ∵潔辣ｧ體ｾ謗･,謖画枚莉ｶ螟ｹ菫晏ｭ倥よｯ泌ｦりｯｴ荳荳ｪ體ｾ謗･譏ｯ https://example.com/1.png,蠖灘燕豢ｻ蜉ｨ蜷肴弍窶憺亢莠醍↓闃ｱ窶晢ｼ碁ぅ荵亥ｰｱ隕∽ｿ晏ｭ伜??output/髦ｴ莠醍↓闃ｱ/example.com/1.png
    public static async Task DownloadAssets(string storyName, PreloadSet assets, CancellationToken ct = default)
    {
        var httpClient = new HttpClient();

        foreach (var asset in assets)
        {
            ct.ThrowIfCancellationRequested();
            var url = asset.Value;
            var fullPath = GetLocalPathFromUrl(storyName, url);
            var directoryPath = Path.GetDirectoryName(fullPath);
            EnsureDirectoryExists(directoryPath!);
            if (!File.Exists(fullPath)) await DownloadFileAsync(httpClient, url, fullPath, ct);
        }
    }

    private static string GetLocalPathFromUrl(string storyName, string url)
    {
        var uri = new Uri(url);
        var localPath = Path.Join(uri.Host, uri.AbsolutePath.TrimStart('/'));
        return Path.Join("output", storyName, localPath);
    }


    public static string GetRelativePathFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        var uri = new Uri(url);
        var localPath = Path.Combine(uri.Host, uri.AbsolutePath.TrimStart('/'));
        return localPath;
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) _ = Directory.CreateDirectory(directoryPath);
    }

    private static async Task DownloadFileAsync(HttpClient httpClient, string url, string fullPath, CancellationToken ct)
    {
        var notice = new NotificationBlock();
        try
        {
            var content = await httpClient.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(fullPath, content, ct);
            notice.RaiseCommonEvent($"Downloaded: {url} to {fullPath}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException httpEx)
        {
            // 螟?逅?鄂醍ｻ懆ｯｷ豎ら嶌蜈ｳ逧?蠑ょｸ?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An error occurred while downloading {url}. Error: {httpEx.Message}"
            ));
        }
        catch (IOException ioEx)
        {
            // 螟?逅?譁?莉ｶ蜀吝?･逶ｸ蜈ｳ逧?蠑ょｸ?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An error occurred while writing to {fullPath}. Error: {ioEx.Message}"
            ));
        }
        catch (Exception ex)
        {
            // 螟?逅?蜈ｶ莉門庄閭ｽ蜿醍函逧?蠑ょｸ?
            notice.OnNetErrorHappen(new NetworkErrorEventArgs(
                $"An unexpected error occurred. Error: {ex.Message}"
            ));
        }
    }
}
