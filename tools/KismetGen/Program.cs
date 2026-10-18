using System.Text.Json;
using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

// 两种用法：
//   KismetGen <asset> <usmap> <function> <variable> <int> <out>
//   KismetGen --json <nodes.json> <asset> <usmap> <function> <out>

static List<KNode> ParseNodes(string json)
{
    using var doc = JsonDocument.Parse(json);
    return ParseList(doc.RootElement.GetProperty("nodes"));
}

static List<KNode> ParseList(JsonElement array)
{
    var nodes = new List<KNode>();

    foreach (var n in array.EnumerateArray())
    {
        var kind = n.GetProperty("kind").GetString();
        switch (kind)
        {
            case "assign":
                nodes.Add(new AssignNode(
                    n.GetProperty("var").GetString()!,
                    n.GetProperty("value").GetInt32()));
                break;
            case "call":
                var args = new List<KArg>();
                if (n.TryGetProperty("args", out var a))
                {
                    foreach (var x in a.EnumerateArray())
                    {
                        if (x.TryGetProperty("int", out var iv)) args.Add(new IntArg(iv.GetInt32()));
                        else if (x.TryGetProperty("var", out var vv)) args.Add(new LocalArg(vv.GetString()!));
                    }
                }
                nodes.Add(new CallNode(n.GetProperty("function").GetString()!, args));
                break;
            case "context-call":
                var cargs = new List<KArg>();
                if (n.TryGetProperty("args", out var ca))
                {
                    foreach (var x in ca.EnumerateArray())
                    {
                        if (x.TryGetProperty("int", out var civ)) cargs.Add(new IntArg(civ.GetInt32()));
                        else if (x.TryGetProperty("var", out var cvv)) cargs.Add(new LocalArg(cvv.GetString()!));
                    }
                }
                nodes.Add(new ContextCallNode(
                    n.GetProperty("object").GetString()!,
                    n.GetProperty("function").GetString()!,
                    cargs));
                break;
            case "if":
            {
                KArg cond = n.GetProperty("cond").TryGetProperty("int", out var cvi)
                    ? new IntArg(cvi.GetInt32())
                    : new LocalArg(n.GetProperty("cond").GetProperty("var").GetString()!);
                var then = n.TryGetProperty("then", out var t) ? ParseList(t) : new List<KNode>();
                nodes.Add(new IfNode(cond, then));
                break;
            }
            case "return":
                nodes.Add(new ReturnNode());
                break;
        }
    }

    return nodes;
}

static int Run(List<KNode> nodes, string assetPath, string usmapPath, string funcName, string outPath)
{
    var asset = new UAsset(assetPath, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
    var fn = asset.Exports.OfType<FunctionExport>().FirstOrDefault(x => x.ObjectName.ToString() == funcName);
    if (fn is null) { Console.Error.WriteLine($"找不到函数: {funcName}"); return 1; }

    var before = fn.ScriptBytecode?.Length ?? 0;
    var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);
    var funcs = ImportIndex.BuildFunctionIndex(asset);

    var exprs = KismetCompiler.CompileToExpressions(nodes, vars, funcs, e => e.GetSize(asset));
    fn.ScriptBytecode = exprs.ToArray();
    asset.Write(outPath);

    Console.WriteLine($"[gen] {funcName}: {before} 条 -> {exprs.Count} 条（{string.Join(" -> ", exprs.Select(x => x.Inst))}）");

    var check = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
    var fn2 = check.Exports.OfType<FunctionExport>().First(x => x.ObjectName.ToString() == funcName);
    Console.WriteLine($"[gen] 读回 {fn2.ScriptBytecode!.Length} 条: {string.Join(" -> ", fn2.ScriptBytecode.Select(x => x.Inst))}");
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

if (args.Length >= 6 && args[0] == "--json")
{
    var nodes = ParseNodes(File.ReadAllText(args[1]));
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
