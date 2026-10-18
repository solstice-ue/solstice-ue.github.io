using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

/// <summary>
/// 用 UAssetAPI 直接读真实蓝图，打印 Kismet 字节码。
/// 通过 SOLSTICE_BP_DIR 指定目录。
/// </summary>
public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 读一个真实蓝图的字节码()
    {
        var dir = Environment.GetEnvironmentVariable("SOLSTICE_BP_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return;
        }

        var files = new[] { Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE") ?? "" }.Where(x => File.Exists(x)).Select(x => new FileInfo(x)).ToArray();

        Console.WriteLine($"[bp] 候选 {files.Length} 个");

        Exception? firstEx = null;

        foreach (var f in files)
        {
            try
            {
                var asset = new UAsset(f.FullName, EngineVersion.VER_UE4_27);
                var withCode = asset.Exports
                    .OfType<FunctionExport>()
                    .Where(x => x.ScriptBytecode is { Length: > 0 })
                    .ToArray();

                if (withCode.Length == 0)
                {
                    continue;
                }

                Console.WriteLine($"[bp] 文件: {f.Name}  ({f.Length:N0} bytes)");
                Console.WriteLine($"[bp] 带字节码的函数导出: {withCode.Length} 个");

                foreach (var fn in withCode.Take(2))
                {
                    Console.WriteLine($"[bp] --- {fn.ObjectName}");
                    var code = fn.ScriptBytecode!;
                    for (var i = 0; i < Math.Min(code.Length, 40); i++)
                    {
                        Console.WriteLine($"    {i:000}  {code[i].Inst}");
                    }

                    if (code.Length > 40)
                    {
                        Console.WriteLine($"    ... 共 {code.Length} 条表达式");
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                firstEx ??= ex;
            }
        }

        Console.WriteLine($"[bp] 首个异常: {firstEx?.GetType().Name}: {firstEx?.Message}");
        Console.WriteLine("[bp] 没找到可解析的带字节码的蓝图");
    }
}
