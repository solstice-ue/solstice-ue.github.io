using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;

namespace Solstice.Core.Tests;

public class CallGenerationTests
{
    [Fact]
    public void 生成函数调用()
    {
        if (!TestAssets.Available) { Console.WriteLine("[call] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var funcs = ImportIndex.BuildFunctionIndex(asset);
        var varName = vars.Keys.First();
        var funcName = funcs.Keys.First(f => f is not "Default__Function");

        var nodes = new List<KNode>
        {
            new CallNode(funcName, [new IntArg(7), new LocalArg(varName)]),
            new ReturnNode()
        };

        var exprs = KismetCompiler.CompileToExpressions(nodes, vars, funcs);
        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        foreach (var e in exprs) ExpressionSerializer.WriteExpression(e, w);
        var bytes = ms.ToArray();

        Console.WriteLine($"[call] {funcName}({varName}) -> {exprs.Count} 条，{bytes.Length} 字节");
        Console.WriteLine($"[call] {BitConverter.ToString(bytes.Take(16).ToArray())}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var first = ExpressionSerializer.ReadExpression(rd);
        Console.WriteLine($"[call] 读回首条 {first.Inst}");
        Assert.Equal("FinalFunction", first.Inst);
    }
}
