using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

/// <summary>
/// 用 UAssetAPI 读真实蓝图的 Kismet 字节码。
/// 样本必须放在可写位置（UAssetAPI 用 File.Open，默认 ReadWrite，只读文件会 Access Denied）。
/// 通过 SOLSTICE_BP_FILE 指定单个 .uasset。
/// </summary>
public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 读真实蓝图的字节码()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE");
        if (string.IsNullOrEmpty(single) || !File.Exists(single))
        {
            Console.WriteLine("[bp] 未指定 SOLSTICE_BP_FILE");
            return;
        }

        Console.WriteLine($"[bp] 文件: {Path.GetFileName(single)}");

        UAsset asset;
        try
        {
            asset = new UAsset(single, EngineVersion.VER_UE5_6);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[bp] 打开失败: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        Console.WriteLine($"[bp] 导出数: {asset.Exports.Count}");

        var funcs = asset.Exports.OfType<FunctionExport>().ToArray();
        Console.WriteLine($"[bp] FunctionExport: {funcs.Length}");

        var withCode = funcs.Where(f => f.ScriptBytecode is { Length: > 0 }).ToArray();
        Console.WriteLine($"[bp] 带字节码: {withCode.Length}");

        foreach (var fn in withCode.Take(2))
        {
            Console.WriteLine($"[bp] --- {fn.ObjectName}  表达式 {fn.ScriptBytecode!.Length} 条");
            var code = fn.ScriptBytecode!;
            for (var i = 0; i < Math.Min(code.Length, 30); i++)
            {
                Console.WriteLine($"    {i:000}  {code[i].Inst}");
            }
        }
    }
}
