using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 打印OnPlayedFromHand字节码()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        Console.WriteLine($"[bp] {Path.GetFileName(single)}  exports={asset.Exports.Count}");

        var funcs = asset.Exports.OfType<FunctionExport>().Where(f => f.ScriptBytecode is { Length: > 0 }).ToArray();
        Console.WriteLine($"[bp] FunctionExport(带字节码)={funcs.Length}");

        var target = funcs.FirstOrDefault(f => f.ObjectName.ToString().Contains("OnPlayedFromHand"));
        if (target is null)
        {
            Console.WriteLine("[bp] 未找到 OnPlayedFromHand，列出前 8 个函数：");
            foreach (var f in funcs.Take(8)) Console.WriteLine($"    {f.ObjectName}  {f.ScriptBytecode!.Length} 条");
            return;
        }

        var code = target.ScriptBytecode!;
        Console.WriteLine($"[bp] === {target.ObjectName}  共 {code.Length} 条 ===");
        for (var i = 0; i < Math.Min(code.Length, 28); i++)
            Console.WriteLine($"    {i:000}  {code[i].Inst}");
    }
}
