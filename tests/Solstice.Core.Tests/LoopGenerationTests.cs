using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class LoopGenerationTests
{
    [Fact]
    public void 生成循环并写回()
    {
        if (!TestAssets.Available) { Console.WriteLine("[lp] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = vars.Keys.First();

        var nodes = new List<KNode>
        {
            new LoopNode(new LocalArg(name), [ new AssignNode(name, new IntArg(5)) ]),
            new ReturnNode()
        };

        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, null, e => e.GetSize(asset));
        Console.WriteLine($"[lp] {exprs.Count} 条: {string.Join(" -> ", exprs.Select(x => x.Inst))}");

        fn.ScriptBytecode = exprs.ToArray();
        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "loop.uasset");
        asset.Write(outPath);

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var back = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1").ScriptBytecode!;
        Console.WriteLine($"[lp] 读回 {back.Length} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");

        var jn = (EX_JumpIfNot)back[0];
        var jump = (EX_Jump)back.First(x => x.Inst == "Jump");
        Console.WriteLine($"[lp] JumpIfNot={jn.CodeOffset}（退出）  Jump={jump.CodeOffset}（跳回）");
        Assert.True(jump.CodeOffset < jn.CodeOffset + 100, "回跳应指向循环开头");
        Assert.True(jn.CodeOffset > jump.CodeOffset, "退出点应在回跳之后");
    }
}
