namespace Solstice.Core.Tests;

/// <summary>
/// 测试用资产副本。
///
/// UAssetAPI 用 File.Open（默认 ReadWrite）打开 .uasset/.uexp/.usmap，且不释放句柄，
/// 多个测试共用同一份文件会互相锁。所以每个测试都复制一份到唯一目录。
/// </summary>
public static class TestAssets
{
    private static readonly string SourceDir = Environment.GetEnvironmentVariable("SOLSTICE_SRC") ?? "";
    private static readonly string UsmapPath = Environment.GetEnvironmentVariable("SOLSTICE_USMAP") ?? "";

    public static bool Available => Directory.Exists(SourceDir) && File.Exists(UsmapPath);

    public sealed record Copy(string AssetPath, string UsmapPath);

    public static Copy Prepare(string assetFileName = "card_brawl_test1.uasset")
    {
        var dir = Path.Combine(Path.GetTempPath(), "solstice-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var asset = Path.Combine(dir, "a.uasset");
        File.Copy(Path.Combine(SourceDir, assetFileName), asset, true);

        var uexp = Path.ChangeExtension(asset, ".uexp");
        File.Copy(Path.ChangeExtension(Path.Combine(SourceDir, assetFileName), ".uexp"), uexp, true);

        var usmap = Path.Combine(dir, "kards.usmap");
        File.Copy(UsmapPath, usmap, true);

        foreach (var f in Directory.GetFiles(dir))
        {
            File.SetAttributes(f, FileAttributes.Normal);
        }

        return new Copy(asset, usmap);
    }
}
