using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 反射看EX_Context结构()
    {
        var single = @"D:\files\projects\bp-scan\card_brawl_test1.uasset";
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        uint off = 0;
        for (var i = 0; i < Math.Min(bc.Length, 10); i++)
        {
            var e = bc[i];
            Console.WriteLine($"[ctx] {i:000} off={off,-4} size={e.GetSize(asset),-4} {e.Inst}");
            foreach (var p in e.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name is "Inst" or "Tag") continue;
                object? v;
                try { v = p.GetValue(e); } catch { v = "<err>"; }
                var s = v?.ToString() ?? "null";
                if (s.Length > 70) s = s[..70] + "...";
                Console.WriteLine($"        {p.Name} = {s}");
            }
            off += e.GetSize(asset);
        }
    }
}
