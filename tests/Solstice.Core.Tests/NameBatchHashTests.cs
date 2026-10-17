using Solstice.AssetRegistry;

namespace Solstice.Core.Tests;

/// <summary>
/// 用真实样本验证 name batch 哈希实现。
///
/// 这是最硬的证据：KARDS 的 bin 里有 3 万多个**原本就算好的**哈希，
/// 我们用 ULocres 的 CityHash 重算一遍，必须逐条吻合。
/// 吻合说明「哈希算法 + 小写化 + ascii/utf16 编码选择」三件事全对。
/// </summary>
public class NameBatchHashTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void 原始样本的name哈希可以逐条重算复现()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var file = File.ReadAllBytes(path);
        var mismatches = NameBatchHasher.CountMismatches(file, out var error, out var count);

        Console.WriteLine($"[hash] name 条数 {count}；不匹配 {mismatches}；错误 {error ?? "无"}");

        Assert.Null(error);
        Assert.True(count > 1000, $"name 条数异常：{count}");
        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void 写回后的哈希表不再是全零()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var doc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(File.ReadAllBytes(path));
        var output = doc.Save();

        var mismatches = NameBatchHasher.CountMismatches(output, out var error, out var count);

        Console.WriteLine($"[hash] 写回后 name 条数 {count}；不匹配 {mismatches}");

        Assert.Null(error);
        Assert.Equal(0, mismatches);
    }
}
