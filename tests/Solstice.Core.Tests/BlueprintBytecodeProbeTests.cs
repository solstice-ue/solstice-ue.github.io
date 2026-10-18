using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 用GetSize验证跳转偏移()
    {
        var single = @"D:\files\projects\bp-scan\card_brawl_test1.uasset";
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));

        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        uint off = 0;
        for (var i = 0; i < bc.Length; i++)
        {
            var size = bc[i].GetSize(asset);
            var target = bc[i] is EX_Jump j ? j.CodeOffset : bc[i] is EX_JumpIfNot jn ? jn.CodeOffset : (uint?)null;
            if (target is { } t)
                Console.WriteLine($"[size] {i:000} {bc[i].Inst,-18} size={size,-4} offset={off,-5} target={t}  -> expr#{FindExpr(bc, asset, t)}");
            off += size;
        }
        Console.WriteLine($"[size] 总字节 = {off}");
    }

    private static string FindExpr(UAssetAPI.Kismet.Bytecode.KismetExpression[] bc, UAsset asset, uint target)
    {
        uint off = 0;
        for (var i = 0; i < bc.Length; i++)
        {
            if (off == target) return i.ToString();
            off += bc[i].GetSize(asset);
        }
        return "?";
    }
}
