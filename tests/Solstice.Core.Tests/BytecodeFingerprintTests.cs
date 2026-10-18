using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;

namespace Solstice.Core.Tests;

public class BytecodeFingerprintTests
{
    [Fact]
    public void 写出的字节与原uexp逐字节一致()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var uexp = Path.ChangeExtension(single, ".uexp");
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        foreach (var e in bc) ExpressionSerializer.WriteExpression(e, w);
        var written = ms.ToArray();
        Console.WriteLine($"[fp] 写出 {written.Length} 字节，表达式 {bc.Length} 条");

        var file = File.ReadAllBytes(uexp);
        var idx = IndexOf(file, written.Take(24).ToArray());
        Console.WriteLine($"[fp] 前 24 字节位置: {(idx >= 0 ? idx.ToString() : "未找到")}");

        if (idx >= 0 && idx + written.Length <= file.Length)
        {
            var same = file.AsSpan(idx, written.Length).SequenceEqual(written);
            Console.WriteLine($"[fp] 整段 {written.Length} 字节比对: 一致={same}");
            if (!same)
            {
                for (var i = 0; i < written.Length; i++)
                {
                    if (file[idx + i] != written[i])
                    {
                        Console.WriteLine($"[fp] 首个差异在段内第 {i} 字节");
                        break;
                    }
                }
            }
        }
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= hay.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
