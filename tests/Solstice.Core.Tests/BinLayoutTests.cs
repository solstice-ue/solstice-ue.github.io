using Solstice.Bin;

namespace Solstice.Core.Tests;

/// <summary>
/// 新 bin 库的分段解析与「未修改即原样写回」契约。
/// 这是后续所有写回能力的地基：任何改动都必须是拼接出来的差异，
/// 而不是重新序列化整份文件。
/// </summary>
public class BinLayoutTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void 分段解析与真实样本一致()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var file = BinFile.Load(bytes);
        var l = file.Layout;

        Console.WriteLine($"[bin] size={l.FileLength:N0} version={l.Version} filterEditorOnly={l.FilterEditorOnly}");
        Console.WriteLine($"[bin] nameCount={l.NameCount:N0} numStringBytes={l.NumStringBytes:N0} hashVersion=0x{l.HashVersion:X}");
        Console.WriteLine($"[bin] headerEnd={l.HeaderEnd} nameBatchEnd={l.NameBatchEnd:N0} storeEnd={l.StoreEnd:N0}");
        Console.WriteLine($"[bin] assetCount={l.AssetCount:N0}");

        Assert.Equal(21, l.Version);
        Assert.Equal(30417, l.NameCount);
        Assert.Equal(0xC1640000UL, l.HashVersion);
        Assert.Equal(16250, l.AssetCount);
        Assert.True(l.HeaderEnd < l.NameBatchEnd && l.NameBatchEnd <= l.StoreEnd);
        Assert.True(l.StoreEnd < l.FileLength);
    }

    [Fact]
    public void 未修改写回逐字节一致()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var output = BinFile.Load(bytes).Save();

        Assert.Equal(bytes.Length, output.Length);
        Assert.True(bytes.AsSpan().SequenceEqual(output), "未修改写回必须逐字节一致");
    }
}
