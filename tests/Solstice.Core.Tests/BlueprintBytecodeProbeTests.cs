using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 带usmap解析卡牌蓝图()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE");
        var usmapPath = Environment.GetEnvironmentVariable("SOLSTICE_USMAP");
        if (string.IsNullOrEmpty(single) || !File.Exists(single)) { Console.WriteLine("[bp] no file"); return; }

        UAsset asset;
        try
        {
            if (!string.IsNullOrEmpty(usmapPath) && File.Exists(usmapPath))
            {
                var mappings = new Usmap(usmapPath);
                asset = new UAsset(single, EngineVersion.VER_UE5_6, mappings);
                Console.WriteLine("[bp] 已加载 usmap");
            }
            else
            {
                asset = new UAsset(single, EngineVersion.VER_UE5_6);
                Console.WriteLine("[bp] 未加载 usmap");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[bp] 失败: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        Console.WriteLine($"[bp] exports={asset.Exports.Count}");
        foreach (var e in asset.Exports.Take(10))
        {
            Console.WriteLine($"    {e.GetType().Name,-18} {e.ObjectName}");
        }
    }
}
