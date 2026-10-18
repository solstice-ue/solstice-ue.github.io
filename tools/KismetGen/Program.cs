using System.Text.Json;
using Solstice.Core.Kismet;
using SolsticePak = Solstice.Core.Pak;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

// 两种用法：
//   KismetGen <asset> <usmap> <function> <variable> <int> <out>
//   KismetGen --json <nodes.json> <asset> <usmap> <function> <out>


static int Run(List<KNode> nodes, string assetPath, string usmapPath, string funcName, string outPath)
{
    // 与页面共用 KismetWorkspace：确保「按需补 Import」等逻辑只有一份实现
    var work = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!, "_kgen");
    var ws = KismetWorkspace.Open(
        File.ReadAllBytes(assetPath),
        File.ReadAllBytes(Path.ChangeExtension(assetPath, ".uexp")),
        File.ReadAllBytes(usmapPath),
        work);

    var before = ws.ExpressionCount(funcName);
    if (before == 0) { Console.Error.WriteLine($"找不到函数或函数无字节码: {funcName}"); return 1; }

    var (ops, hex, bytes) = ws.Generate(nodes, funcName);
    Console.WriteLine($"[gen] {funcName}: {before} 条 -> {ops.Count} 条（{string.Join(" -> ", ops)}）");

    var saved = ws.Save(work, Path.GetFileName(outPath));
    File.WriteAllBytes(outPath, saved.UAsset);
    File.WriteAllBytes(Path.ChangeExtension(outPath, ".uexp"), saved.UExp);
    Console.WriteLine($"[gen] 已写出 {outPath}（uasset {saved.UAsset.Length} / uexp {saved.UExp.Length} 字节）");

    var check = KismetWorkspace.Open(saved.UAsset, saved.UExp, File.ReadAllBytes(usmapPath), work + "-check");
    Console.WriteLine($"[gen] 读回 {check.ExpressionCount(funcName)} 条（函数总数 {check.Functions.Count}）");
    return 0;
}

if (args.Length >= 3 && args[0] == "--list")
{
    // 列出资产里的函数与某个函数的可用变量，方便拼命令
    var listAsset = new UAsset(args[1], EngineVersion.VER_UE5_6, new Usmap(args[2]));
    var fns = listAsset.Exports.OfType<FunctionExport>()
        .Where(x => x.ScriptBytecode is { Length: > 0 })
        .ToArray();

    Console.WriteLine($"[list] 带字节码的函数 {fns.Length} 个");
    foreach (var f in fns.Take(20))
    {
        Console.WriteLine($"  {f.ObjectName}  ({f.ScriptBytecode!.Length} 条)");
    }

    Console.WriteLine("[list] 可调用的函数导入:");
    foreach (var kv in ImportIndex.BuildFunctionIndex(listAsset).Take(12))
    {
        Console.WriteLine($"  {kv.Key} -> {kv.Value.Index}");
    }

    if (fns.Length > 0)
    {
        var vars = ExpressionWalker.BuildVariableIndex(fns[0].ScriptBytecode!);
        Console.WriteLine($"[list] {fns[0].ObjectName} 的变量 ({vars.Count} 个):");
        foreach (var k in vars.Keys.Take(12))
        {
            Console.WriteLine($"  {k}");
        }
    }

    return 0;
}

if (args.Length >= 4 && args[0] == "--pak")
{
    // KismetGen --pak <out.pak> <mountPoint> <目录>
    var outPak = args[1];
    var mount = args[2];
    var dir = Path.GetFullPath(args[3]);

    var entries = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
        .OrderBy(x => x, StringComparer.Ordinal)
        .Select(x => new SolsticePak.PakEntry(Path.GetRelativePath(dir, x).Replace('\\', '/'), File.ReadAllBytes(x)))
        .ToList();

    var bytes = SolsticePak.PakWriter.Build(mount, entries);
    File.WriteAllBytes(outPak, bytes);
    Console.WriteLine($"[pak] mount='{mount}'  {entries.Count} 个条目 -> {outPak}（{bytes.Length} 字节）");
    foreach (var en in entries) Console.WriteLine($"  {en.Name}  ({en.Data.Length})");
    return 0;
}
if (args.Length >= 6 && args[0] == "--json")
{
    var nodes = KismetWorkspace.ParseNodes(File.ReadAllText(args[1]));
    return Run(nodes, args[2], args[3], args[4], args[5]);
}

if (args.Length >= 6)
{
    var nodes = new List<KNode> { new AssignNode(args[3], int.Parse(args[4])), new ReturnNode() };
    return Run(nodes, args[0], args[1], args[2], args[5]);
}

Console.Error.WriteLine("用法: KismetGen <asset> <usmap> <function> <variable> <int> <out>");
Console.Error.WriteLine("  或: KismetGen --json <nodes.json> <asset> <usmap> <function> <out>");
return 2;
