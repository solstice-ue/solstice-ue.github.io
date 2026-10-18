using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class BytecodeShapeTests
{
    [Fact]
    public void JumpIfNot是否内嵌条件()
    {
        var single = @"D:\files\projects\bp-scan\card_brawl_test1.uasset";
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        foreach (var e in bc.Where(x => x.Inst is "JumpIfNot" or "Jump").Take(3))
        {
            Console.WriteLine($"[shape] {e.Inst} size={e.GetSize(asset)}");
            foreach (var f in e.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name is "Inst") continue;
                var v = f.GetValue(e);
                var extra = v is UAssetAPI.Kismet.Bytecode.KismetExpression sub ? $" (sub size={sub.GetSize(asset)})" : "";
                Console.WriteLine($"    {f.Name} = {v}{extra}");
            }
        }
    }
}
