using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

/// <summary>
/// 带 owner 的外部函数调用：需要补齐 包 → 类 → 函数 三级 Import。
/// </summary>
public class OwnerChainTests
{
    [Fact]
    public void 按owner补齐三级Import()
    {
        if (!TestAssets.Available) { Console.WriteLine("[own] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var before = asset.Imports.Count;
        Console.WriteLine($"[own] 原始 Imports={before}");

        // /Script/Engine 已作为包存在（-44）；Actor 类不存在；函数也不存在
        var fnIdx = ImportIndex.EnsureFunction(asset, "K2_GetActorLocation", "/Script/Engine.Actor");
        Console.WriteLine($"[own] 函数索引={fnIdx.Index}，Imports 现在 {asset.Imports.Count} 条");

        for (var i = before; i < asset.Imports.Count; i++)
        {
            var im = asset.Imports[i];
            Console.WriteLine($"[own] 新增 #{i} idx={-(i + 1)} {im.ObjectName}  cls={im.ClassName}  outer={im.OuterIndex?.Index}");
        }

        // 校验链：函数 -> 类 -> 包
        var fnImport = asset.Imports[-fnIdx.Index - 1];
        var classIdx = fnImport.OuterIndex!.Index;
        var classImport = asset.Imports[-classIdx - 1];
        var pkgIdx = classImport.OuterIndex!.Index;
        var pkgImport = asset.Imports[-pkgIdx - 1];
        Console.WriteLine($"[own] 链: {fnImport.ObjectName}(Function) -> {classImport.ObjectName}(Class) -> {pkgImport.ObjectName}(Package)");

        Assert.Equal("Actor", classImport.ObjectName.ToString());
        Assert.Equal("/Script/Engine", pkgImport.ObjectName.ToString());
        Assert.Equal(-44, pkgIdx);
    }
}
