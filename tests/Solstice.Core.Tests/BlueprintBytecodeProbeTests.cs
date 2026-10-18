using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class BlueprintBytecodeProbeTests
{
    [Fact]
    public void 找带跳转的函数()
    {
        var dir = Environment.GetEnvironmentVariable("SOLSTICE_BP_DIR")!;
        var usmapPath = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var tmp = @"D:\files\projects\bp-scan";
        Directory.CreateDirectory(tmp);

        var files = Directory.GetFiles(dir, "card_*.uasset", SearchOption.AllDirectories).Take(400).ToArray();
        Console.WriteLine($"[jump] 扫描 {files.Length} 个");

        foreach (var f in files)
        {
            var dst = Path.Combine(tmp, Path.GetFileName(f));
            try
            {
                File.Copy(f, dst, true);
                var uexp = Path.ChangeExtension(f, ".uexp");
                if (File.Exists(uexp)) File.Copy(uexp, Path.ChangeExtension(dst, ".uexp"), true);

                var asset = new UAsset(dst, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
                foreach (var fn in asset.Exports.OfType<FunctionExport>())
                {
                    var bc = fn.ScriptBytecode;
                    if (bc is null || bc.Length == 0) continue;
                    if (!bc.Any(x => x.Inst == "JumpIfNot" || x.Inst == "Jump")) continue;

                    Console.WriteLine($"[jump] {Path.GetFileName(f)} :: {fn.ObjectName}  共 {bc.Length} 条");
                    for (var i = 0; i < bc.Length; i++)
                    {
                        var marker = bc[i].Inst is "JumpIfNot" or "Jump" ? "  <<<" : "";
                        var op = bc[i] is UAssetAPI.Kismet.Bytecode.Expressions.EX_Jump j ? $"codeOffset={j.CodeOffset}" : bc[i] is UAssetAPI.Kismet.Bytecode.Expressions.EX_JumpIfNot jn ? $"codeOffset={jn.CodeOffset}" : ""; Console.WriteLine($"    {i:000}  {bc[i].Inst}{marker}   {op}");
                    }
                    return;
                }
            }
            catch { }
        }
        Console.WriteLine("[jump] 没找到");
    }
}
