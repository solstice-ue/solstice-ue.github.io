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
    var nodes = new List<KNode>();

    foreach (var n in doc.RootElement.GetProperty("nodes").EnumerateArray())
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
