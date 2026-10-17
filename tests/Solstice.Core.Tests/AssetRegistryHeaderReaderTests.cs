using Solstice.Core.AssetRegistry;
using Solstice.Core.IO;

namespace Solstice.Core.Tests;

/// <summary>
/// 头部读取器的测试。样本是**合成字节数组**，不是游戏资产——
/// 真实样本不得进仓库（见 CONTRIBUTING.md）。
/// </summary>
public class AssetRegistryHeaderReaderTests
{
    /// <summary>现代格式的 AssetRegistry 标识，与读取器内部常量一致。</summary>
    private static readonly byte[] Magic =
    [
        0xE7, 0x9E, 0x7F, 0x71, 0x3A, 0x49, 0xB0, 0xE9,
        0x32, 0x91, 0xB3, 0x88, 0x07, 0x81, 0x38, 0x1B
    ];

    private static byte[] BuildHeader(int version, int filterEditorOnly, byte[]? guid = null)
    {
        var data = new byte[AssetRegistryHeaderReader.HeaderSize];
        (guid ?? Magic).CopyTo(data, 0);
        BitConverter.TryWriteBytes(data.AsSpan(16, 4), version);
        BitConverter.TryWriteBytes(data.AsSpan(20, 4), filterEditorOnly);
        return data;
    }

    [Fact]
    public void 读取_KARDS_实测头部()
    {
        // 与 docs/samples.md 记录的实测值一致：version 21、bFilterEditorOnly 1。
        var data = BuildHeader(version: 21, filterEditorOnly: 1);

        var ok = AssetRegistryHeaderReader.TryRead(data, out var header, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(header);
        Assert.True(header!.GuidMatches);
        Assert.Equal(21, header.Version);
        Assert.True(header.FilterEditorOnly);
        Assert.Equal("E79E7F713A49B0E93291B3880781381B", header.GuidHex);
    }

    [Fact]
    public void bFilterEditorOnly_为_0_时应读成_false()
    {
        var data = BuildHeader(version: 21, filterEditorOnly: 0);

        Assert.True(AssetRegistryHeaderReader.TryRead(data, out var header, out _));
        Assert.False(header!.FilterEditorOnly);
    }

    [Fact]
    public void GUID_不匹配不算读取失败()
    {
        var wrong = new byte[16];
        var data = BuildHeader(version: 21, filterEditorOnly: 1, guid: wrong);

        var ok = AssetRegistryHeaderReader.TryRead(data, out var header, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(header!.GuidMatches);
        Assert.Equal("00000000000000000000000000000000", header.GuidHex);
    }

    [Fact]
    public void 数据不足时返回失败并给出原因()
    {
        var data = new byte[10];

        var ok = AssetRegistryHeaderReader.TryRead(data, out var header, out var error);

        Assert.False(ok);
        Assert.Null(header);
        Assert.NotNull(error);
        Assert.Contains("10", error);
        Assert.Contains("24", error);
    }

    [Fact]
    public void SHA256_对已知输入给出已知结果()
    {
        // 空字节数组的 SHA-256 是公开已知值。
        Assert.Equal(
            "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            FileFingerprint.Sha256(ReadOnlySpan<byte>.Empty));
    }
}
