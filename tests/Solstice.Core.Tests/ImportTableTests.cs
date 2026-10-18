using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class ImportTableTests
{
    [Fact]
    public void 查看Import表结构()
    {
        if (!TestAssets.Available) { Console.WriteLine("[imp] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        Console.WriteLine($"[imp] Imports={asset.Imports.Count}  Exports={asset.Exports.Count}");
        foreach (var im in asset.Imports.Take(6))
        {
            Console.WriteLine($"[imp] {im.ClassName}  {im.ObjectName}  outer={im.OuterIndex.Index}");
        }

        // 找函数类导入
        var funcs = asset.Imports.Where(i => i.ClassName?.ToString() == "Function").Take(5).ToArray();
        Console.WriteLine($"[imp] ClassName=Function 的导入 {funcs.Length} 条");
        foreach (var f in funcs)
        {
            Console.WriteLine($"[imp]   {f.ObjectName}  outer={f.OuterIndex.Index}");
        }
    }
}
