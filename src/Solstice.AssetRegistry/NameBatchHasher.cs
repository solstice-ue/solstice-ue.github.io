using System.Buffers.Binary;
using System.Text;
using ULocres;

namespace Solstice.AssetRegistry;

/// <summary>
/// 修复 AssetRegistry.bin 里 name batch 的哈希表。
///
/// <para>
/// <b>为什么需要它</b>：上游 UAssetRegistry 的写入器把 hashVersion 与所有哈希都写成 0，
/// 注释说「读取时忽略」——对它的读取器成立，但 UE 用这些哈希查名字，
/// 全 0 会导致写出的 bin 在游戏里读不了。
/// </para>
///
/// <para>
/// 布局（现代格式，紧接 24 字节头）：
/// <c>uint32 count | uint32 numStringBytes | uint64 hashVersion | uint64 hashes[count] | 2 字节头[count] | 字符串区</c>。
/// 哈希 = CityHash64(小写名，ascii 或 utf-16-le 字节)，由 <see cref="ULocres.CityHash"/> 计算
/// （该实现逐行对齐 UE）。
/// </para>
/// </summary>
public static class NameBatchHasher
{
    /// <summary>UE 的 name batch 哈希版本号。</summary>
    public const ulong HashVersion = 0xC1640000UL;

    /// <summary>文件头长度：GUID(16) + version(4) + bFilterEditorOnly(4)。</summary>
    private const int HeaderSize = 24;

    /// <summary>就地重算并写回哈希表与 hashVersion。返回 false 表示结构不符合预期。</summary>
    public static bool TryPatch(byte[] file, out string? error)
    {
        error = null;

        if (!TryGetLayout(file, out var layout, out error))
        {
            return false;
        }

        if (layout.Count == 0)
        {
            return true;
        }

        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(layout.HashVersionOffset, 8), HashVersion);

        for (var i = 0; i < layout.Count; i++)
        {
            var headerOffset = layout.HeadersOffset + i * 2;
            var b0 = file[headerOffset];
            var b1 = file[headerOffset + 1];
            var isUtf16 = ((b0 >> 7) & 1) == 1;
            var length = ((b0 & 0x7F) << 8) | b1;

            var byteLen = isUtf16 ? length * 2 : length;
            var nameBytes = file.AsSpan(layout.StringsOffset + layout.StringCursor, byteLen);
            layout.StringCursor += byteLen;

            var name = isUtf16
                ? Encoding.Unicode.GetString(nameBytes)
                : Encoding.ASCII.GetString(nameBytes);

            var hash = ComputeNameHash(name, isUtf16);
            BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(layout.HashesOffset + i * 8, 8), hash);
        }

        return true;
    }

    /// <summary>
    /// 校验用：把文件里已存的哈希与重算结果逐条比对，返回不匹配的条数。
    /// 用真实样本跑一次，就能证明哈希实现是对的。
    /// </summary>
    public static int CountMismatches(byte[] file, out string? error, out int count)
    {
        count = 0;
        error = null;

        if (!TryGetLayout(file, out var layout, out error))
        {
            return -1;
        }

        count = layout.Count;
        var mismatches = 0;

        for (var i = 0; i < layout.Count; i++)
        {
            var headerOffset = layout.HeadersOffset + i * 2;
            var b0 = file[headerOffset];
            var b1 = file[headerOffset + 1];
            var isUtf16 = ((b0 >> 7) & 1) == 1;
            var length = ((b0 & 0x7F) << 8) | b1;

            var byteLen = isUtf16 ? length * 2 : length;
            var nameBytes = file.AsSpan(layout.StringsOffset + layout.StringCursor, byteLen);
            layout.StringCursor += byteLen;

            var name = isUtf16
                ? Encoding.Unicode.GetString(nameBytes)
                : Encoding.ASCII.GetString(nameBytes);

            var expected = ComputeNameHash(name, isUtf16);
            var actual = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(layout.HashesOffset + i * 8, 8));

            if (expected != actual)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    private static ulong ComputeNameHash(string name, bool isUtf16)
    {
        var lower = ToLowerAsciiOnly(name);
        var bytes = isUtf16 ? Encoding.Unicode.GetBytes(lower) : Encoding.ASCII.GetBytes(lower);
        return CityHash.Hash64(bytes);
    }

    /// <summary>
    /// 只把 ASCII 的 A-Z 转小写，其余字符原样保留。
    ///
    /// <para>
    /// 这不是随便定的：UE 的 FName 小写化只处理 ASCII 范围，
    /// 用 <c>string.ToLowerInvariant</c> 会把西里尔/带音标字符也改掉，
    /// 导致 20 个含非 ASCII 的名字哈希对不上（实测如此）。
    /// </para>
    /// </summary>
    private static string ToLowerAsciiOnly(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= 'A' && c <= 'Z')
            {
                chars[i] = (char)(c + 32);
            }
        }

        return new string(chars);
    }

    private sealed class Layout
    {
        public int Count;
        public int HashVersionOffset;
        public int HashesOffset;
        public int HeadersOffset;
        public int StringsOffset;
        public int StringCursor;
    }

    private static bool TryGetLayout(byte[] file, out Layout layout, out string? error)
    {
        layout = new Layout();
        error = null;

        if (file.Length < HeaderSize + 4)
        {
            error = "文件太短，读不到 name batch。";
            return false;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderSize, 4));
        if (count <= 0)
        {
            layout.Count = 0;
            return true;
        }

        var numStringBytes = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderSize + 4, 4));
        var hashVersionOffset = HeaderSize + 8;
        var hashesOffset = hashVersionOffset + 8;
        var headersOffset = hashesOffset + count * 8;
        var stringsOffset = headersOffset + count * 2;
        var end = stringsOffset + numStringBytes;

        if (count < 0 || numStringBytes < 0 || end > file.Length)
        {
            error = $"name batch 越界：count={count}, numStringBytes={numStringBytes}, 需要 {end} 字节，实际 {file.Length}。";
            return false;
        }

        layout.Count = count;
        layout.HashVersionOffset = hashVersionOffset;
        layout.HashesOffset = hashesOffset;
        layout.HeadersOffset = headersOffset;
        layout.StringsOffset = stringsOffset;
        layout.StringCursor = 0;
        return true;
    }
}
