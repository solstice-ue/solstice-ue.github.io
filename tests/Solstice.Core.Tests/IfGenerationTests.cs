using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class IfGenerationTests
{
    [Fact]
    public void 生成条件分支并写回()
    {
        if (!TestAssets.Available) { Console.WriteLine("[if] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = vars.Keys.First();

        var nodes = new List<KNode>
        {
            new IfNode(new LocalArg(name), [ new ReturnNode() ]),
            new ReturnNode()
        };

        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, null, e => e.GetSize(asset));
        Console.WriteLine($"[if] {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "if.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var back = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1").ScriptBytecode!;
        Console.WriteLine($"[if] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var jn = (EX_JumpIfNot)back[0];
        var j = (EX_Jump)back.First(x => x.Inst == "Jump");
        Console.WriteLine($"[if] JumpIfNot.CodeOffset={jn.CodeOffset}  Jump.CodeOffset={j.CodeOffset}");
        Assert.True(jn.CodeOffset > 0, "JumpIfNot 的偏移应大于 0");
        Assert.True(j.CodeOffset > jn.CodeOffset, "Jump 应跳到 JumpIfNot 之后");
    }
}
