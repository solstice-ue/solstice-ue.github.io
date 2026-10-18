using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class LoopProbeTests
{
    [Fact]
    public void 找带反向跳转的函数()
    {
        if (!TestAssets.Available) { Console.WriteLine("[loop] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        foreach (var fn in asset.Exports.OfType<FunctionExport>())
        {
            var bc = fn.ScriptBytecode;
            if (bc is null || bc.Length == 0) continue;

            uint off = 0;
            var found = new List<string>();
            for (var i = 0; i < bc.Length; i++)
            {
                var size = bc[i].GetSize(asset);
                var target = bc[i] is EX_Jump j ? j.CodeOffset : bc[i] is EX_JumpIfNot jn ? jn.CodeOffset : (uint?)null;
                if (target is { } t2 && t2 < off)
                {
                    found.Add($"#{i} {bc[i].Inst} off={off} -> {t2}（反向）");
                }
                off += size;
            }

            if (found.Count > 0)
            {
                Console.WriteLine($"[loop] {fn.ObjectName}（{bc.Length} 条）");
                foreach (var f in found.Take(4)) Console.WriteLine($"[loop]   {f}");
            }
        }

        Console.WriteLine("[loop] 扫描完成");
    }
}
