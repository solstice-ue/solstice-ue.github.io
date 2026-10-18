using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;

namespace Solstice.Core.Tests;

public class VariableGenerationTests
{
    [Fact]
    public void 用真实变量指针生成LocalVariable()
    {
        if (!TestAssets.Available) { Console.WriteLine("[var] 跳过：无样本"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var index = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
        var name = index.Keys.First();

        var nodes = new List<KNode> { new CallNode("Probe", [new LocalArg(name)]) };
        var exprs = KismetCompiler.CompileToExpressions(nodes, index);

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        foreach (var e in exprs) ExpressionSerializer.WriteExpression(e, w);
        var bytes = ms.ToArray();

        Console.WriteLine($"[var] 变量 {name} -> {exprs.Count} 条表达式，{bytes.Length} 字节");
        Console.WriteLine($"[var] 首条 {exprs[0].Inst}，字节 {BitConverter.ToString(bytes.Take(10).ToArray())}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = ExpressionSerializer.ReadExpression(rd);
        Console.WriteLine($"[var] 读回 {back.Inst}");
        Assert.Equal("LocalVariable", back.Inst);
    }
}
