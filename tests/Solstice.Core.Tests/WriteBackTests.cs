using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class WriteBackTests
{
    [Fact]
    public void 替换函数字节码并写回资产()
    {
        if (!TestAssets.Available) { Console.WriteLine("[wb] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        Console.WriteLine($"[wb] 原字节码 {fn.ScriptBytecode!.Length} 条");

        // 替换成 return 42;
        fn.ScriptBytecode =
        [
            new EX_Return { ReturnExpression = new EX_IntConst { Value = 42 } },
            new EX_EndOfScript()
        ];

        var outPath = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "out.uasset");
        asset.Write(outPath);
        Console.WriteLine($"[wb] 已写出 {outPath}（{new FileInfo(outPath).Length} 字节）");

        var reloaded = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var fn2 = reloaded.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        Console.WriteLine($"[wb] 读回字节码 {fn2.ScriptBytecode!.Length} 条: {string.Join(" -> ", fn2.ScriptBytecode.Select(x => x.Inst))}");

        Assert.Equal(2, fn2.ScriptBytecode.Length);
        Assert.Equal(42, ((EX_IntConst)((EX_Return)fn2.ScriptBytecode[0]).ReturnExpression).Value);
    }
}
