using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

// 用法: KismetGen <asset.uasset> <kards.usmap> <函数名> <变量名> <整数值> <输出.uasset>
if (args.Length < 6)
{
    Console.Error.WriteLine("用法: KismetGen <asset> <usmap> <function> <variable> <int> <out>");
    return 2;
}

var (assetPath, usmapPath, funcName, varName, valueText, outPath) =
    (args[0], args[1], args[2], args[3], args[4], args[5]);

var asset = new UAsset(assetPath, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
var fn = asset.Exports.OfType<FunctionExport>()
    .FirstOrDefault(x => x.ObjectName.ToString() == funcName);

if (fn is null)
{
    Console.Error.WriteLine($"找不到函数: {funcName}");
    return 1;
}

var before = fn.ScriptBytecode?.Length ?? 0;
var vars = ExpressionWalker.BuildVariableIndex(fn.ScriptBytecode!);

if (!vars.ContainsKey(varName))
{
    Console.Error.WriteLine($"函数里没有变量 {varName}；可用: {string.Join(", ", vars.Keys.Take(8))}");
    return 1;
}

var nodes = new List<KNode>
{
    new AssignNode(varName, int.Parse(valueText)),
    new ReturnNode()
};

var exprs = KismetCompiler.CompileToExpressions(nodes, vars);
fn.ScriptBytecode = exprs.ToArray();
asset.Write(outPath);

Console.WriteLine($"[gen] {funcName}: {before} 条 -> {exprs.Count} 条（{string.Join(" -> ", exprs.Select(x => x.Inst))}）");
Console.WriteLine($"[gen] 已写出 {outPath}");

// 读回校验
var check = new UAsset(outPath, EngineVersion.VER_UE5_6, new Usmap(usmapPath));
var fn2 = check.Exports.OfType<FunctionExport>().First(x => x.ObjectName.ToString() == funcName);
Console.WriteLine($"[gen] 读回 {fn2.ScriptBytecode!.Length} 条: {string.Join(" -> ", fn2.ScriptBytecode.Select(x => x.Inst))}");
return 0;
