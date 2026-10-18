using Solstice.Core.Kismet;

namespace Solstice.Core.Tests;

/// <summary>
/// 新增 Import 条目后，产物是否仍然可重复写出且导出数量不变。
/// 这是结构改动最大的场景，最容易出现不自洽。
/// </summary>
public class ImportStabilityTests
{
    [Fact]
    public void 新增Import后产物仍稳定()
    {
        if (!TestAssets.Available) { Console.WriteLine("[is] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var usmap = File.ReadAllBytes(copy.UsmapPath);
        var work = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "is");

        var ws = KismetWorkspace.Open(
            File.ReadAllBytes(copy.AssetPath),
            File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp")),
            usmap, work);

        var before = ws.FunctionImports.Count;
        Console.WriteLine($"[is] 原始函数导入 {before} 个");

        var json = """
        { "nodes": [
          { "kind": "call", "function": "K2_GetActorLocation", "owner": "/Script/Engine.Actor", "args": [] },
          { "kind": "return" }
        ] }
        """;

        var (ops, hex, bytes) = ws.Generate(json, "ExecuteUbergraph_card_brawl_test1");
        Console.WriteLine($"[is] 生成 {ops.Count} 条: {string.Join(" -> ", ops)}");

        var first = ws.Save(work, "p1.uasset");
        var ws2 = KismetWorkspace.Open(first.UAsset, first.UExp, usmap, work + "2");
        var second = ws2.Save(work + "2", "p2.uasset");

        var sameAsset = first.UAsset.AsSpan().SequenceEqual(second.UAsset);
        var sameExp = first.UExp.AsSpan().SequenceEqual(second.UExp);
        Console.WriteLine($"[is] 两次写出 uasset 一致={sameAsset}  uexp 一致={sameExp}");
        Console.WriteLine($"[is] 重新打开后函数导入 {ws2.FunctionImports.Count} 个，函数 {ws2.Functions.Count} 个");

        Assert.True(sameAsset, "uasset 应可重复写出");
        Assert.True(sameExp, "uexp 应可重复写出");
        Assert.True(ws2.FunctionImports.ContainsKey("K2_GetActorLocation"), "新导入应保留");
        Assert.Equal(19, ws2.Functions.Count);
    }
}
