using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

public class ImportChainTests
{
    [Fact]
    public void 打印完整Import链()
    {
        if (!TestAssets.Available) { Console.WriteLine("[chain] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var im = asset.Imports[i];
            Console.WriteLine($"[chain] #{i,-2} idx={-(i + 1),-3} {im.ObjectName}  cls={im.ClassName}  outer={im.OuterIndex?.Index}  pkg={im.PackageName}");
        }
    }
}
