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
        var single = @"D:\files\projects\bp-tmp\t1\a.uasset";
        var uexp = @"D:\files\projects\bp-tmp\t1\a.uexp";
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
        var probe = written.Take(24).ToArray();
        var idx = IndexOf(file, probe);
        Console.WriteLine($"[fp] 前 24 字节在 uexp 位置: {(idx >= 0 ? idx.ToString() : "未找到")}");

        if (idx >= 0 && idx + written.Length <= file.Length)
        {
            var same = file.AsSpan(idx, written.Length).SequenceEqual(written);
            Console.WriteLine($"[fp] 整段 {written.Length} 字节比对: 一致={same}");
            Assert.True(same, "写出的字节应与原文件一致");
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
