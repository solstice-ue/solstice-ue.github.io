using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class WorkspaceTests
{
    [Fact]
    public void 写回两半后重新打开导出数量保持()
    {
        if (!TestAssets.Available) { Console.WriteLine("[ws] 跳过：无样本"); return; }

        var copy = TestAssets.Prepare();
        var uasset = File.ReadAllBytes(copy.AssetPath);
        var uexp = File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp"));
        var usmap = File.ReadAllBytes(copy.UsmapPath);
        var work = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "ws");

        var before = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var beforeExports = before.Exports.Count;
        var beforeFuncs = before.Exports.OfType<FunctionExport>().Count(x => x.ScriptBytecode is { Length: > 0 });
        Console.WriteLine($"[ws] 原始 exports={beforeExports} 带字节码函数={beforeFuncs}");

        var ws = KismetWorkspace.Open(uasset, uexp, usmap, work);
        var fn = "ExecuteUbergraph_card_brawl_test1";
        var vars = ws.Variables(fn);

        var json = $$"""
        { "nodes": [
          { "kind": "assign", "var": "{{vars[0]}}", "value": 11 },
          { "kind": "return" }
        ] }
        """;

        var (opcodes, hex, bytes) = ws.Generate(json, fn);
        Console.WriteLine($"[ws] 生成 {opcodes.Count} 条 / {bytes} 字节: {string.Join(" -> ", opcodes)}");

        var saved = ws.Save(work);
        Console.WriteLine($"[ws] 保存 uasset={saved.UAsset.Length} uexp={saved.UExp.Length}");

        // 用两半重新打开
        var work2 = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "ws2");
        var ws2 = KismetWorkspace.Open(saved.UAsset, saved.UExp, usmap, work2);
        var afterFuncs = ws2.Functions.Count;
        Console.WriteLine($"[ws] 重新打开后带字节码函数={afterFuncs}");

        // 直接按路径核对
        var after = new UAsset(Path.Combine(work, "out.uasset"), EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        Console.WriteLine($"[ws] 写回后 exports={after.Exports.Count} 带字节码函数={after.Exports.OfType<FunctionExport>().Count(x => x.ScriptBytecode is { Length: > 0 })}");

        Assert.Equal(beforeExports, after.Exports.Count);
        Assert.Equal(beforeFuncs, afterFuncs);
    }
}
