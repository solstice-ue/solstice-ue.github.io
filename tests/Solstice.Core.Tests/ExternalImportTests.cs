using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

/// <summary>
/// 调用一个**尚未出现在 Import 表**里的外部函数：
/// 生成器应当自动补一条 Import，并让 StackNode 指向它。
/// </summary>
public class ExternalImportTests
{
    [Fact]
    public void 调用未导入的函数会自动补Import()
    {
        if (!TestAssets.Available) { Console.WriteLine("[ext] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var uasset = File.ReadAllBytes(copy.AssetPath);
        var uexp = File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp"));
        var usmap = File.ReadAllBytes(copy.UsmapPath);
        var work = Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "ext");

        var before = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        var importsBefore = before.Imports.Count;
        Console.WriteLine($"[ext] 原始 Imports={importsBefore}");

        var ws = KismetWorkspace.Open(uasset, uexp, usmap, work);
        var fn = "ExecuteUbergraph_card_brawl_test1";

        // 这个函数不在导入表里（原有导入只有 IsValid / GetChooseSpawnCards 等）
        const string target = "K2_GetActorLocation";
        Console.WriteLine($"[ext] 目标函数 {target} 是否已在导入表: {ws.FunctionImports.ContainsKey(target)}");

        var json = $$"""
        { "nodes": [
          { "kind": "call", "function": "{{target}}", "args": [] },
          { "kind": "return" }
        ] }
        """;

        var (opcodes, hex, bytes) = ws.Generate(json, fn);
        Console.WriteLine($"[ext] 生成 {opcodes.Count} 条 / {bytes} 字节: {string.Join(" -> ", opcodes)}");

        var saved = ws.Save(work);
        Console.WriteLine($"[ext] 保存 uasset={saved.UAsset.Length} uexp={saved.UExp.Length}");

        var after = new UAsset(Path.Combine(work, "out.uasset"), EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));
        Console.WriteLine($"[ext] 写回后 Imports={after.Imports.Count}（新增 {after.Imports.Count - importsBefore} 条）");

        var added = after.Imports.LastOrDefault();
        Console.WriteLine($"[ext] 新增条目: Object={added?.ObjectName} Class={added?.ClassName} ClassPkg={added?.ClassPackage} Outer={added?.OuterIndex?.Index}");

        var fn2 = after.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == fn);
        var call = (EX_FinalFunction)fn2.ScriptBytecode![0];
        Console.WriteLine($"[ext] 调用 StackNode={call.StackNode.Index}");

        Assert.True(after.Imports.Count > importsBefore, "应新增一条 Import");
        Assert.Equal(target, added!.ObjectName.ToString());
        Assert.Equal(call.StackNode.Index, -(after.Imports.Count));
    }
}
