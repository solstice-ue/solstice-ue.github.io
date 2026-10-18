using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 扫卡牌找带字节码的()
    {
        var dir = Environment.GetEnvironmentVariable("SOLSTICE_BP_DIR")!;
        var usmapPath = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var tmp = @"D:\files\projects\bp-scan";
        Directory.CreateDirectory(tmp);

        var files = Directory.GetFiles(dir, "card_*.uasset", SearchOption.AllDirectories);
        Console.WriteLine($"[scan] 卡牌 {files.Length} 个");

        var hits = 0;
        foreach (var f in files)
        {
            var name = Path.GetFileName(f);
            var dst = Path.Combine(tmp, name);
            File.Copy(f, dst, true);
            var uexp = Path.ChangeExtension(f, ".uexp");
            if (File.Exists(uexp)) File.Copy(uexp, Path.ChangeExtension(dst, ".uexp"), true);

            try
            {
                var asset = new UAsset(dst, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
                var funcs = asset.Exports.OfType<FunctionExport>().Where(x => x.ScriptBytecode is { Length: > 0 }).ToArray();
                if (funcs.Length == 0) continue;

                hits++;
                Console.WriteLine($"[scan] {name}: exports={asset.Exports.Count} 函数={funcs.Length}");

                var target = funcs.FirstOrDefault(x => x.ObjectName.ToString().Contains("OnPlayedFromHand"));
                if (target is not null)
                {
                    var bc = target.ScriptBytecode!;
                    Console.WriteLine($"[scan] === {target.ObjectName} 共 {bc.Length} 条 ===");
                    for (var i = 0; i < Math.Min(bc.Length, 30); i++)
                        Console.WriteLine($"    {i:000}  {bc[i].Inst}");
                    return;
                }

                if (hits >= 3) return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[scan] {name} 失败: {ex.GetType().Name}");
            }
        }

        Console.WriteLine($"[scan] 命中 {hits} 个");
    }
}
