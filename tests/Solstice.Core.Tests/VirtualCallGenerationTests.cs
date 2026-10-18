using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class VirtualCallGenerationTests
{
    [Fact]
    public void 生成虚函数调用并写回()
    {
        if (!TestAssets.Available) { Console.WriteLine("[vc] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = vars.Keys.First();

        // 用函数自身已有的名字，确保在资产名字表里
        var funcName = fn.ObjectName.ToString();

        var nodes = new List<KNode>
        {
            new VirtualCallNode(funcName, [new LocalArg(name)]),
            new ReturnNode()
        };

        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, null, null, n => new FName(asset, n));
        Console.WriteLine($"[vc] {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "virt.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var back = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1").ScriptBytecode!;
        Console.WriteLine($"[vc] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var v = (EX_LocalVirtualFunction)back[0];
        Console.WriteLine($"[vc] 名字={v.VirtualFunctionName}  参数 {v.Parameters.Length} 个");
        Assert.Equal("LocalVirtualFunction", back[0].Inst);
        Assert.Equal(funcName, v.VirtualFunctionName.ToString());
    }
}
