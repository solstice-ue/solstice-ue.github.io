using System.Buffers.Binary;

namespace Solstice.Core.AssetRegistry;

/// <summary>
/// 读取 AssetRegistry.bin 的固定头部：16 字节 GUID + int32 version + int32 bFilterEditorOnly。
///
/// <para>
/// 证据：KARDS 的真实样本（6.2 MB）实测 version = 21、bFilterEditorOnly = 1，
/// 开头 GUID 与本类的 <see cref="MagicGuid"/> 一致。见 docs/samples.md。
/// </para>
///
/// <para>
/// <b>本类刻意只做这一件事。</b>name 表、FStore、资产数组、依赖节点与包数据
/// 都尚未在 WASM 下验证，不属于本类的职责，也不在此处做任何猜测性解析。
/// </para>
/// </summary>
public static class AssetRegistryHeaderReader
{
    /// <summary>头部长度：16（GUID）+ 4（version）+ 4（bFilterEditorOnly）。</summary>
    public const int HeaderSize = 24;

    /// <summary>
    /// UE 的 AssetRegistry 文件标识。
    ///
    /// <para>
    /// 这是<b>现代格式</b>的标识。更老的 AssetRegistry 版本使用不同的开头，
    /// 本读取器不覆盖它们；不匹配时只会把 <see cref="AssetRegistryHeader.GuidMatches"/>
    /// 置为 false，而不会假装读懂了文件。
    /// </para>
    /// </summary>
    private static readonly byte[] MagicGuid =
    [
        0xE7, 0x9E, 0x7F, 0x71, 0x3A, 0x49, 0xB0, 0xE9,
        0x32, 0x91, 0xB3, 0x88, 0x07, 0x81, 0x38, 0x1B
    ];

    /// <summary>
    /// 尝试读取头部。数据不足时返回 false 并给出原因；GUID 不匹配<b>不算</b>读取失败，
    /// 而是通过 <see cref="AssetRegistryHeader.GuidMatches"/> 如实反映。
    /// </summary>
    public static bool TryRead(
        ReadOnlySpan<byte> data,
        out AssetRegistryHeader? header,
        out string? error)
    {
        header = null;
        error = null;

        if (data.Length < HeaderSize)
        {
            error = $"文件太短：只有 {data.Length} 字节，至少需要 {HeaderSize} 字节才能读到头部。";
            return false;
        }

        var guid = data[..16];
        var guidMatches = guid.SequenceEqual(MagicGuid);
        var version = BinaryPrimitives.ReadInt32LittleEndian(data[16..20]);
        var filterEditorOnly = BinaryPrimitives.ReadInt32LittleEndian(data[20..24]) != 0;

        header = new AssetRegistryHeader(
            GuidMatches: guidMatches,
            Version: version,
            FilterEditorOnly: filterEditorOnly,
            GuidHex: Convert.ToHexString(guid));

        return true;
    }
}
