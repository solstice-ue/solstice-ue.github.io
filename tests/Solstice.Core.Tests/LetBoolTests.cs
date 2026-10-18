using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class LetBoolTests
{
    [Fact]
    public void 生成布尔赋值并写回()
    {
        if (!TestAssets.Available) { Console.WriteLine("[lb] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = vars.Keys.First();

        var nodes = new List<KNode> { new AssignBoolNode(name, true), new ReturnNode() };
        var exprs = KismetCompiler.CompileToExpressions(nodes, vars);
        Console.WriteLine($"[lb] {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "bool.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var back = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1").ScriptBytecode!;
        Console.WriteLine($"[lb] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var lb = (EX_LetBool)back[0];
        Console.WriteLine($"[lb] 变量={lb.VariableExpression.Inst}  值={lb.AssignmentExpression.Inst}");
        Assert.Equal("LetBool", back[0].Inst);
        Assert.Equal("True", lb.AssignmentExpression.Inst);
    }
}
