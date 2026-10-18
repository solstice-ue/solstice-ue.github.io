using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class WalkerTests
{
    [Fact]
    public void 递归收集变量表达式()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var all = ExpressionWalker.Walk(bc).ToArray();
        var vars = ExpressionWalker.CollectVariables(bc);

        Console.WriteLine($"[walk] 顶层 {bc.Length} 条，递归展开 {all.Length} 条，变量 {vars.Count} 个");
        foreach (var v in vars.Take(4))
        {
            var p = v.Variable;
            Console.WriteLine($"[walk] {v.Inst}  Old={(p?.Old is null ? "null" : p.Old.Index.ToString())}  New={(p?.New is null ? "null" : "有")}");
        }
    }
}
