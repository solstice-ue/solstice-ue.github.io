using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class ContextGenerationTests
{
    [Fact]
    public void 生成跨对象调用并写回()
    {
        if (!TestAssets.Available) { Console.WriteLine("[cg] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var funcs = ImportIndex.BuildFunctionIndex(asset);
        var objVar = vars.Keys.First();
        var target = funcs.Keys.First(f => f is not "Default__Function");

        var nodes = new List<KNode>
        {
            new ContextCallNode(objVar, target, [new IntArg(3)]),
            new ReturnNode()
        };

        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, funcs, e => e.GetSize(asset));
        Console.WriteLine($"[cg] {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "ctx.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn2 = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var back = fn2.ScriptBytecode!;
        Console.WriteLine($"[cg] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var ctx = (EX_Context)back[0];
        Console.WriteLine($"[cg] obj={ctx.ObjectExpression.Inst} ctx={ctx.ContextExpression.Inst} Offset={ctx.Offset}");
        Assert.Equal("Context", back[0].Inst);
        Assert.True(ctx.Offset > 0);
    }
}
