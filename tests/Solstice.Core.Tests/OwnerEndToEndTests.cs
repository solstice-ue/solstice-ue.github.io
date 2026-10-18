using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

/// <summary>
/// 端到端：JSON 里带 owner 的 call，写回后函数挂在正确的类下面。
/// </summary>
public class OwnerEndToEndTests
{
    [Fact]
    public void JSON带owner调用未导入函数()
    {
        if (!TestAssets.Available) { Console.WriteLine("[e2e] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var uasset = File.ReadAllBytes(copy.AssetPath);
        var uexp = File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp"));
        var usmap = File.ReadAllBytes(copy.UsmapPath);
        var work = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "own");

        var ws = KismetWorkspace.Open(uasset, uexp, usmap, work);
        var fn = "ExecuteUbergraph_card_brawl_test1";

        var json = """
        { "nodes": [
          { "kind": "call", "function": "K2_GetActorLocation", "owner": "/Script/Engine.Actor", "args": [] },
          { "kind": "return" }
        ] }
        """;

        var (opcodes, hex, bytes) = ws.Generate(json, fn);
        Console.WriteLine($"[e2e] 生成 {opcodes.Count} 条 / {bytes} 字节: {string.Join(" -> ", opcodes)}");

        var saved = ws.Save(work);
        var after = new UAsset(Path.Combine(work, "out.uasset"), EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        Console.WriteLine($"[e2e] 写回后 Imports={after.Imports.Count}");

        // 找到新函数导入，验证它的 outer 是 Actor 类，Actor 的 outer 是 /Script/Engine
        var fnImport = after.Imports.FirstOrDefault(x => x.ObjectName?.ToString() == "K2_GetActorLocation");
        Console.WriteLine($"[e2e] 函数导入 outer={fnImport?.OuterIndex?.Index}");
        var cls = after.Imports[-fnImport!.OuterIndex!.Index - 1];
        Console.WriteLine($"[e2e] 所属类 {cls.ObjectName} outer={cls.OuterIndex?.Index}");
        var pkg = after.Imports[-cls.OuterIndex!.Index - 1];
        Console.WriteLine($"[e2e] 所属包 {pkg.ObjectName}");

        var fnExport = after.Exports.OfType<FunctionExport>().First(x => x.ObjectName.ToString() == fn);
        var call = (EX_FinalFunction)fnExport.ScriptBytecode![0];
        Console.WriteLine($"[e2e] 调用 StackNode={call.StackNode.Index}（应等于函数导入的索引）");

        Assert.Equal("Actor", cls.ObjectName.ToString());
        Assert.Equal("/Script/Engine", pkg.ObjectName.ToString());
        Assert.Equal(-(after.Imports.IndexOf(fnImport) + 1), call.StackNode.Index);
    }
}
