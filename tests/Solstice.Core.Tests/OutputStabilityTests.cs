using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

/// <summary>
/// 产物自洽性：生成的资产再加载、再写出，字节是否保持一致。
/// 若不一致，说明结构里有非确定性（例如名字表顺序），可能影响引擎加载。
/// </summary>
public class OutputStabilityTests
{
    [Fact]
    public void 生成结果可否重复写出()
    {
        if (!TestAssets.Available) { Console.WriteLine("[st] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var usmap = File.ReadAllBytes(copy.UsmapPath);
        var work = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "st");

        var ws = KismetWorkspace.Open(
            File.ReadAllBytes(copy.AssetPath),
            File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp")),
            usmap, work);

        var json = """
        { "nodes": [
          { "kind": "assign", "var": "EntryPoint", "value": 5 },
          { "kind": "return" }
        ] }
        """;

        var (ops, hex, bytes) = ws.Generate(json, "ExecuteUbergraph_card_brawl_test1");
        var first = ws.Save(work, "pass1.uasset");
        Console.WriteLine($"[st] 第一次写出 uasset={first.UAsset.Length} uexp={first.UExp.Length}");

        // 用第一次的产物重新打开，再写出一次
        var work2 = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "st2");
        var ws2 = KismetWorkspace.Open(first.UAsset, first.UExp, usmap, work2);
        var second = ws2.Save(work2, "pass2.uasset");
        Console.WriteLine($"[st] 第二次写出 uasset={second.UAsset.Length} uexp={second.UExp.Length}");

        var sameAsset = first.UAsset.AsSpan().SequenceEqual(second.UAsset);
        var sameExp = first.UExp.AsSpan().SequenceEqual(second.UExp);
        Console.WriteLine($"[st] uasset 一致={sameAsset}  uexp 一致={sameExp}");

        if (!sameAsset)
        {
            var n = Math.Min(first.UAsset.Length, second.UAsset.Length);
            for (var i = 0; i < n; i++)
            {
                if (first.UAsset[i] != second.UAsset[i])
                {
                    Console.WriteLine($"[st] 首个差异在字节 {i}");
                    break;
                }
            }
        }
    }
}
