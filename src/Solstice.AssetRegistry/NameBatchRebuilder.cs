using System.Buffers.Binary;
using System.Text;
using ULocres;

namespace Solstice.AssetRegistry;

/// <summary>
/// 重建 AssetRegistry.bin 的 name batch，使其符合 UE 的真实格式。
///
/// <para>
/// <b>为什么需要它</b>：上游 UAssetRegistry 的写入器有三处与 UE 不符，
/// 任何一处都会让游戏读不了这个文件：
/// </para>
/// <list type="number">
///   <item><c>numStringBytes</c> 写成 0 —— UE 用它切字符串区，0 等于没有名字；</item>
///   <item>name header 里存 UTF-8 字节长度、且没有 UTF-16 标志位；</item>
///   <item>哈希表与 hashVersion 全 0 —— UE 靠哈希查名字。</item>
/// </list>
///
/// <para>
/// 做法：把写入器产出的 name batch 解出来，按 UE 格式重新编码，
/// 再原位替换该段。其后的 FStore / 资产数组等字节原样保留，只整体平移。
/// </para>
///
/// <para>
/// UE 格式：<c>uint32 count | uint32 numStringBytes | uint64 hashVersion(0xC1640000)
/// | uint64 hashes[count] | 2 字节头[count] | 字符串区</c>。
/// 头部 = <c>(isUtf16 &lt;&lt; 7) | (len &gt;&gt; 8)</c> 与 <c>len &amp; 0xFF</c>，
/// 其中 len 对 UTF-16 名是**字符数**、对 ASCII 名是字节数。
/// </para>
/// </summary>
public static class NameBatchRebuilder
{
    public const ulong HashVersion = 0xC1640000UL;

    private const int HeaderSize = 24;
    private const uint FStoreBeginMagic = 0x12345679u;

    /// <summary>重建 name batch，返回新文件字节。失败时返回 null 并给出原因。</summary>
    public static byte[]? Rebuild(byte[] file, out string? error)
    {
        error = null;

        if (file.Length < HeaderSize + 4)
        {
            error = "文件太短，读不到 name batch。";
            return null;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderSize, 4));
        if (count <= 0)
        {
            return file; // 空 name 表，无需处理
        }

        var hashesOffset = HeaderSize + 16;
        var headersOffset = hashesOffset + count * 8;
        var stringsOffset = headersOffset + count * 2;

        if (stringsOffset > file.Length)
        {
            error = $"name batch 越界：count={count}，需要 {stringsOffset} 字节，实际 {file.Length}。";
            return null;
        }

        // 1) 按写入器的格式解出名字：header 是 UTF-8 字节长度，字符串区是 UTF-8。
        var names = new string[count];
        var cursor = stringsOffset;
        for (var i = 0; i < count; i++)
        {
            var b0 = file[headersOffset + i * 2];
            var b1 = file[headersOffset + i * 2 + 1];
            var len = ((b0 & 0x7F) << 8) | b1;

            if (cursor + len > file.Length)
            {
                error = $"第 {i} 个名字越界：len={len}，剩余 {file.Length - cursor}。";
                return null;
            }

            names[i] = Encoding.UTF8.GetString(file, cursor, len);
            cursor += len;
        }

        var oldSectionEnd = cursor;

        // 2) 按 UE 格式重新编码
        var hashBytes = new byte[count * 8];
        var headerBytes = new byte[count * 2];
        var stringBytes = new List<byte>(oldSectionEnd - stringsOffset + 64);

        for (var i = 0; i < count; i++)
        {
            var name = names[i];
            var isAscii = IsPureAscii(name);

            byte[] raw;
            int lengthField;

            if (isAscii)
            {
                raw = Encoding.ASCII.GetBytes(name);
                lengthField = raw.Length;          // ASCII：字节数
            }
            else
            {
                raw = Encoding.Unicode.GetBytes(name);
                lengthField = name.Length;          // UTF-16：字符数
            }

            if (lengthField > 0x7FFF)
            {
                error = $"名字过长（{lengthField}）：{name[..Math.Min(32, name.Length)]}…";
                return null;
            }

            var flag = isAscii ? 0 : 1;
            headerBytes[i * 2] = (byte)((flag << 7) | ((lengthField >> 8) & 0x7F));
            headerBytes[i * 2 + 1] = (byte)(lengthField & 0xFF);

            stringBytes.AddRange(raw);

            var hash = CityHash.Hash64(isAscii ? Encoding.ASCII.GetBytes(ToLowerAsciiOnly(name))
                                               : Encoding.Unicode.GetBytes(ToLowerAsciiOnly(name)));
            BinaryPrimitives.WriteUInt64LittleEndian(hashBytes.AsSpan(i * 8, 8), hash);
        }

        // 3) 拼出新段：count + numStringBytes + hashVersion + hashes + headers + strings
        var newSection = new byte[4 + 4 + 8 + hashBytes.Length + headerBytes.Length + stringBytes.Count];
        var span = newSection.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span[..4], count);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..8], stringBytes.Count);
        BinaryPrimitives.WriteUInt64LittleEndian(span[8..16], HashVersion);
        hashBytes.CopyTo(span[16..]);
        headerBytes.CopyTo(span[(16 + hashBytes.Length)..]);
        stringBytes.CopyTo(span[(16 + hashBytes.Length + headerBytes.Length)..]);

        // 4) 原位替换：头部之后接新段，再接原来的剩余部分
        var tail = file.AsSpan(oldSectionEnd);
        var result = new byte[HeaderSize + newSection.Length + tail.Length];
        file.AsSpan(0, HeaderSize).CopyTo(result);
        newSection.CopyTo(result.AsSpan(HeaderSize));
        tail.CopyTo(result.AsSpan(HeaderSize + newSection.Length));

        return result;
    }

    /// <summary>校验：重算每个名字的哈希，与文件里存的值比对，返回不匹配条数。</summary>
    public static int CountMismatches(byte[] file, out string? error, out int count)
    {
        count = 0;
        error = null;

        if (file.Length < HeaderSize + 16)
        {
            error = "文件太短。";
            return -1;
        }

        var n = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderSize, 4));
        if (n <= 0)
        {
            return 0;
        }

        var numStringBytes = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderSize + 4, 4));
        var hashVersion = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(HeaderSize + 8, 8));
        var hashesOffset = HeaderSize + 16;
        var headersOffset = hashesOffset + n * 8;
        var stringsOffset = headersOffset + n * 2;

        if (stringsOffset + numStringBytes > file.Length)
        {
            error = $"name batch 越界：count={n}, numStringBytes={numStringBytes}。";
            return -1;
        }

        count = n;

        if (hashVersion != HashVersion)
        {
            error = $"hashVersion 不是 0x{HashVersion:X}，实际 0x{hashVersion:X}";
            return -1;
        }

        var mismatches = 0;
        var cursor = stringsOffset;

        for (var i = 0; i < n; i++)
        {
            var b0 = file[headersOffset + i * 2];
            var b1 = file[headersOffset + i * 2 + 1];
            var isUtf16 = ((b0 >> 7) & 1) == 1;
            var length = ((b0 & 0x7F) << 8) | b1;
            var byteLen = isUtf16 ? length * 2 : length;

            var name = isUtf16
                ? Encoding.Unicode.GetString(file, cursor, byteLen)
                : Encoding.ASCII.GetString(file, cursor, byteLen);
            cursor += byteLen;

            var lower = ToLowerAsciiOnly(name);
            var expected = CityHash.Hash64(isUtf16 ? Encoding.Unicode.GetBytes(lower) : Encoding.ASCII.GetBytes(lower));
            var actual = BinaryPrimitives.ReadUInt64LittleEndian(file.AsSpan(hashesOffset + i * 8, 8));

            if (expected != actual)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    private static bool IsPureAscii(string s)
    {
        foreach (var c in s)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 只把 ASCII 的 A-Z 转小写。UE 的 FName 小写化不处理非 ASCII，
    /// 用 <c>ToLowerInvariant</c> 会让含非 ASCII 的名字哈希对不上（实测 20 条不符）。
    /// </summary>
    private static string ToLowerAsciiOnly(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= 'A' && chars[i] <= 'Z')
            {
                chars[i] = (char)(chars[i] + 32);
            }
        }

        return new string(chars);
    }
}
