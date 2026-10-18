using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;

namespace Solstice.Core.Tests;

public class BytecodeSizeDiffTests
{
    [Fact]
    public void 逐条对比GetSize与Write返回()
    {
        var single = @"D:\files\projects\bp-tmp\t1\a.uasset";
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var shown = 0;
        uint totalGet = 0;
        var totalWrite = 0;

        foreach (var e in bc)
        {
            try
            {
                var g = e.GetSize(asset);
                var ms = new MemoryStream();
                var w = new AssetBinaryWriter(ms, asset);
                var ret = ExpressionSerializer.WriteExpression(e, w);
                var actual = ms.ToArray().Length;

                totalGet += g;
                totalWrite += actual;

                if (actual != g && shown < 8)
                {
                    shown++;
                    Console.WriteLine($"[diff] {e.Inst,-22} GetSize={g,-4} writeRet={ret,-4} bytes={actual}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[diff] {e.Inst} 抛 {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"[diff] 合计 GetSize={totalGet} 实际写={totalWrite} 差={totalWrite - totalGet}");
    }
}
