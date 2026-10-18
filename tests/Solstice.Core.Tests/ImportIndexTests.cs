using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

public class ImportIndexTests
{
    [Fact]
    public void 函数名到FPackageIndex()
    {
        if (!TestAssets.Available) { Console.WriteLine("[fi] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var index = ImportIndex.BuildFunctionIndex(asset);
        Console.WriteLine($"[fi] 函数导入索引 {index.Count} 项");
        foreach (var kv in index.Take(5))
        {
            Console.WriteLine($"[fi] {kv.Key} -> {kv.Value.Index}");
        }

        Assert.NotEmpty(index);
        Assert.True(index.Values.All(v => v.Index < 0), "Import 索引应为负数");
    }
}
