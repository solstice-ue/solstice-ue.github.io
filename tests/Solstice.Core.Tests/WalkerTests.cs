using Solstice.Core.Kismet;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class WalkerTests
{
    [Fact]
    public void 递归收集变量表达式()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");
        var bc = fn.ScriptBytecode!;

        var all = ExpressionWalker.Walk(bc).ToArray();
        var vars = ExpressionWalker.CollectVariables(bc);

        Console.WriteLine($"[walk] 顶层 {bc.Length} 条，递归展开 {all.Length} 条，变量 {vars.Count} 个");
        foreach (var v in vars.Take(4))
        {
            var p = v.Variable;
            var path = p?.New;
            if (path is not null)
            {
                foreach (var fld in path.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    var val = fld.GetValue(path);
                    var s = val is System.Collections.IEnumerable en and not string
                        ? string.Join("|", en.Cast<object>().Select(x => x?.ToString()))
                        : val?.ToString();
                    if (s is not null && s.Length > 70) s = s[..70] + "...";
                    Console.WriteLine($"[walk]   {fld.Name}({fld.FieldType.Name}) = {s}");
                }
            }
        }
    }
}
