namespace Solstice.Core.Tests;

/// <summary>
/// 测试用资产副本。
///
/// UAssetAPI 用 File.Open（默认 ReadWrite）打开 .uasset/.uexp/.usmap，且不释放句柄，
/// 多个测试共用同一份文件会互相锁。所以每个测试都复制一份到唯一目录。
/// </summary>
public static class TestAssets
{
    /// <summary>复制期间的互斥：并行测试同时 File.Copy 同一个源文件会互相干扰。</summary>
    private static readonly object Sync = new();
    private static readonly string SourceDir = Environment.GetEnvironmentVariable("SOLSTICE_SRC") ?? "";
    private static readonly string UsmapPath = Environment.GetEnvironmentVariable("SOLSTICE_USMAP") ?? "";

    public static bool Available => Directory.Exists(SourceDir) && File.Exists(UsmapPath);

    public sealed record Copy(string AssetPath, string UsmapPath);

    public static Copy Prepare(string assetFileName = "card_brawl_test1.uasset")
    {
        // 不要用 Path.GetTempPath()：本机沙箱对临时目录写入会 Access Denied。
        // 默认落在工作区下的固定目录，可用 SOLSTICE_TEST_TMP 覆盖。
        var baseDir = Environment.GetEnvironmentVariable("SOLSTICE_TEST_TMP")
                      ?? @"D:\files\projects\bp-tests";
        var dir = Path.Combine(baseDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var asset = Path.Combine(dir, "a.uasset");
        var uexp = Path.ChangeExtension(asset, ".uexp");
        var usmap = Path.Combine(dir, "kards.usmap");

        // 构建刚结束时源文件句柄可能还没释放，重试几次再放弃
        static void CopyWithRetry(string from, string to)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Copy(from, to, true);
                    return;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(100);
                }
            }
        }

        lock (Sync)
        {
            CopyWithRetry(Path.Combine(SourceDir, assetFileName), asset);
            CopyWithRetry(Path.ChangeExtension(Path.Combine(SourceDir, assetFileName), ".uexp"), uexp);
            CopyWithRetry(UsmapPath, usmap);

            foreach (var f in Directory.GetFiles(dir))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }
        }

        return new Copy(asset, usmap);
    }
}
