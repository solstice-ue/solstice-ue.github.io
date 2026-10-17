namespace Solstice.Bin;

/// <summary>
/// 一份 AssetRegistry.bin。
///
/// <para>
/// 设计原则：**未修改就逐字节原样写回**。任何「读进来再重新序列化」的做法都会
/// 改变字节（name 表重建、tag 重编码），而游戏对格式的容忍度未知——实测已因此失败过。
/// 所以写回以原始字节为基底，只对改动区段做拼接。
/// </para>
/// </summary>
public sealed class BinFile
{
    private readonly byte[] _original;

    private BinFile(byte[] original, BinLayout layout)
    {
        _original = original;
        Layout = layout;
    }

    public BinLayout Layout { get; }

    public int AssetCount => Layout.AssetCount;

    public static BinFile Load(byte[] data)
    {
        if (!BinLayout.TryParse(data, out var layout, out var error))
        {
            throw new InvalidOperationException($"无法解析 AssetRegistry.bin：{error}");
        }

        return new BinFile(data, layout!);
    }

    /// <summary>当前实现的写回：原样返回。后续的追加能力在此之上拼接。</summary>
    public byte[] Save() => (byte[])_original.Clone();
}
