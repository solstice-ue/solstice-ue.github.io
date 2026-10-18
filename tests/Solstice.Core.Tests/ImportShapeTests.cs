using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

public class ImportShapeTests
{
    [Fact]
    public void 函数导入的完整字段()
    {
        if (!TestAssets.Available) { Console.WriteLine("[imp] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        Console.WriteLine($"[imp] Imports={asset.Imports.Count}");
        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var im = asset.Imports[i];
            if (im.ClassName?.ToString() != "Function") continue;

            Console.WriteLine($"[imp] #{i} idx={-(i + 1)}  Object={im.ObjectName}");
            Console.WriteLine($"[imp]     ClassPackage={im.ClassPackage}  ClassName={im.ClassName}");
            Console.WriteLine($"[imp]     Outer={im.OuterIndex?.Index}  PackageName={im.PackageName}  Optional={im.bImportOptional}");
            if (i > 12) break;
        }

        // 顺带看看名字表里有没有可用来测试的外部函数名
        Console.WriteLine($"[imp] 名字表条目数={asset.GetNameMapIndexList().Count}");
    }
}
