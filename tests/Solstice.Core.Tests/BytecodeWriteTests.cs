using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;

namespace Solstice.Core.Tests;

public class BytecodeWriteTests
{
    [Fact]
    public void 写出再读回是否一致()
    {
        var single = @"D:\files\projects\bp-tmp\t1\a.uasset";
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var expect = 0u;
        foreach (var e in bc) expect += e.GetSize(asset);

        using var ms = new MemoryStream();
        using (var w = new AssetBinaryWriter(ms, asset))
        {
            foreach (var e in bc) ExpressionSerializer.WriteExpression(e, w);
        }

        Console.WriteLine($"[wr] 表达式 {bc.Length} 条，写 {ms.Length} 字节，GetSize 合计 {expect}，一致={ms.Length == expect}");

        ms.Position = 0;
        using var rd = new AssetBinaryReader(ms, asset);
        var back = new List<KismetExpression>();
        for (var i = 0; i < bc.Length; i++) back.Add(ExpressionSerializer.ReadExpression(rd));

        var same = back.Count == bc.Length && back.Select(x => x.Inst).SequenceEqual(bc.Select(x => x.Inst));
        Console.WriteLine($"[wr] 读回 {back.Count} 条，指令序列一致={same}");
        Assert.True(same);
        Assert.Equal((int)expect, (int)ms.Length);
    }
}
