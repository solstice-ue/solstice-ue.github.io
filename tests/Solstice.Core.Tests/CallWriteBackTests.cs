using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class CallWriteBackTests
{
    [Fact]
    public void 生成调用并写回资产()
    {
        if (!TestAssets.Available) { Console.WriteLine("[cw] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var funcs = ImportIndex.BuildFunctionIndex(asset);
        var funcName = funcs.Keys.First(f => f is not "Default__Function");
        var varName = vars.Keys.First();

        var nodes = new List<KNode>
        {
            new CallNode(funcName, [new IntArg(7), new LocalArg(varName)]),
            new ReturnNode()
        };
        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, funcs);
        Console.WriteLine($"[cw] 生成 {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();

        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "call.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn2 = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var back = fn2.ScriptBytecode!;
        Console.WriteLine($"[cw] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        Assert.Equal("FinalFunction", back[0].Inst);
        var call = (EX_FinalFunction)back[0];
        Console.WriteLine($"[cw] StackNode={call.StackNode.Index}  参数 {call.Parameters.Length} 个");
        Assert.Equal(2, call.Parameters.Length);
    }
}
