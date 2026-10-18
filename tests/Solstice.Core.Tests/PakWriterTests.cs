using System.Security.Cryptography;
using System.Text;
using Solstice.Core.Pak;

namespace Solstice.Core.Tests;

/// <summary>
/// 用 PakWriter 重建样本 pak，与原文件逐字节比对。
/// 一致即证明对该变体格式的理解无偏差。
/// </summary>
public class PakWriterTests
{
    private const string SamplePath = @"D:\files\Debug_P1.61.pak";

    [Fact]
    public void 重建样本pak()
    {
        if (!File.Exists(SamplePath)) { Console.WriteLine("[pak] 跳过：无样本"); return; }

        var original = File.ReadAllBytes(SamplePath);
        Console.WriteLine($"[pak] 原文件 {original.Length} 字节");

        // 解析 footer
        var f = original.Length - 44;
        var magic = BitConverter.ToUInt32(original, f);
        var ver = BitConverter.ToInt32(original, f + 4);
        var idxOff = BitConverter.ToInt64(original, f + 8);
        var idxSize = BitConverter.ToInt64(original, f + 16);
        Console.WriteLine($"[pak] magic=0x{magic:X8} version={ver} idxOff={idxOff} idxSize={idxSize}");
        Assert.Equal(PakWriter.Magic, magic);
        Assert.Equal(PakWriter.Version, ver);

        // 解析 index
        var idx = new byte[idxSize];
        Array.Copy(original, idxOff, idx, 0, idxSize);
        var pos = 0;
        var mount = ReadFString(idx, ref pos);
        var count = BitConverter.ToInt32(idx, pos); pos += 4;
        Console.WriteLine($"[pak] mount='{mount}' count={count}");

        var entries = new List<PakEntry>();
        for (var i = 0; i < count; i++)
        {
            var name = ReadFString(idx, ref pos);
            var off = BitConverter.ToInt64(idx, pos);
            var size = BitConverter.ToInt64(idx, pos + 8);
            pos += 53; // 整个 FPakEntry

            var data = new byte[size];
            Array.Copy(original, off + 53, data, 0, size);
            entries.Add(new PakEntry(name, data));
            Console.WriteLine($"[pak] 条目 {i}: {name}  off={off} size={size}");
        }
        Assert.Equal(idxSize, pos);

        // 重建
        var rebuilt = PakWriter.Build(mount, entries);
        Console.WriteLine($"[pak] 重建 {rebuilt.Length} 字节");

        var same = rebuilt.AsSpan().SequenceEqual(original);
        Console.WriteLine($"[pak] 与原文件一致: {same}");

        if (!same && rebuilt.Length == original.Length)
        {
            for (var i = 0; i < original.Length; i++)
            {
                if (rebuilt[i] != original[i])
                {
                    Console.WriteLine($"[pak] 首个差异在 {i}: 原={original[i]:x2} 新={rebuilt[i]:x2}");
                    break;
                }
            }
        }

        Assert.True(same, "重建结果应与原文件逐字节相同");
    }

    private static string ReadFString(byte[] b, ref int p)
    {
        var len = BitConverter.ToInt32(b, p); p += 4;
        if (len == 0) return "";
        if (len > 0)
        {
            var s = Encoding.ASCII.GetString(b, p, len); p += len;
            return s.TrimEnd('\0');
        }
        var c = -len;
        var u = Encoding.Unicode.GetString(b, p, c * 2); p += c * 2;
        return u.TrimEnd('\0');
    }
}
