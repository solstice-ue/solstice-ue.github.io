using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class WalkerTests
{
    [Fact]
    public void 递归收集变量表达式并建索引()
    {
        if (!TestAssets.Available) { Console.WriteLine("[walk] 跳过：无样本"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var all = ExpressionWalker.Walk(bc).ToArray();
        var index = ExpressionWalker.BuildVariableIndex(bc);

        Console.WriteLine($"[walk] 顶层 {bc.Length} 条 -> 递归 {all.Length} 条；变量索引 {index.Count} 项");
        foreach (var kv in index.Take(4))
        {
            Console.WriteLine($"[walk] {kv.Key}  owner={kv.Value.New?.ResolvedOwner?.Index}");
        }

        Assert.True(all.Length > bc.Length, "递归展开应多于顶层");
        Assert.NotEmpty(index);
    }
}
