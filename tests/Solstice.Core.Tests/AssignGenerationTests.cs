using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class AssignGenerationTests
{
    [Fact]
    public void 生成赋值并写回资产()
    {
        if (!TestAssets.Available) { Console.WriteLine("[as] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = vars.Keys.First();

        var nodes = new List<KNode> { new AssignNode(name, 9), new ReturnNode() };
        var exprs = KismetCompiler.CompileToExpressions(nodes, vars);
        Console.WriteLine($"[as] 生成 {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "assign.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn2 = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var back = fn2.ScriptBytecode!;
        Console.WriteLine($"[as] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var let = (EX_Let)back[0];
        Console.WriteLine($"[as] 赋值值 = {((EX_IntConst)let.Expression).Value}");
        Assert.Equal(9, ((EX_IntConst)let.Expression).Value);
    }
}
