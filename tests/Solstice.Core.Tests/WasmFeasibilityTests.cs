using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

public class WasmFeasibilityTests
{
    [Fact]
    public void 纯内存构造资产()
    {
        if (!TestAssets.Available) { Console.WriteLine("[wasm] 跳过：无样本"); return; }

        // TestAssets.Prepare 复制出来的 usmap 是可写的；源文件带只读属性
        var copy = TestAssets.Prepare();
        Console.WriteLine($"[wasm] 可写副本: {copy.UsmapPath}");

        Usmap? usmap = null;

        // 路线 A：先把字节写到可写路径，再按路径构造（模拟浏览器虚拟文件系统）
        try
        {
            var scratch = Path.Combine(Path.GetDirectoryName(copy.UsmapPath)!, "mapped.usmap");
            File.WriteAllBytes(scratch, File.ReadAllBytes(copy.UsmapPath));
            usmap = new Usmap(scratch);
            Console.WriteLine("[wasm] 路线A File.WriteAllBytes + Usmap(path) 成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[wasm] 路线A 失败: {ex.GetType().Name}: {ex.Message}");
        }

        // 路线 B：直接对副本路径构造
        if (usmap is null)
        {
            try
            {
                usmap = new Usmap(copy.UsmapPath);
                Console.WriteLine("[wasm] 路线B Usmap(可写副本路径) 成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[wasm] 路线B 失败: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (usmap is null)
        {
            Console.WriteLine("[wasm] 结论：Usmap 构造失败");
            return;
        }

        var assetBytes = File.ReadAllBytes(copy.AssetPath);
        var uexpBytes = File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp"));
        var combined = new byte[assetBytes.Length + uexpBytes.Length];
        assetBytes.CopyTo(combined, 0);
        uexpBytes.CopyTo(combined, assetBytes.Length);

        try
        {
            var reader = new AssetBinaryReader(new MemoryStream(combined), true);
            var asset = new UAsset(reader, EngineVersion.VER_UE5_6, usmap);
            Console.WriteLine($"[wasm] 拼接流打开成功: Exports={asset.Exports.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[wasm] 拼接流失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
