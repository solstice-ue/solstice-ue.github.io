using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class PropertyPointerTests
{
    [Fact]
    public void 真实LocalVariable的指针布局()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));
        var fn = asset.Exports.OfType<FunctionExport>()
            .First(x => x.ObjectName.ToString() == "ExecuteUbergraph_card_brawl_test1");

        var lv = fn.ScriptBytecode!.OfType<EX_VariableBase>().FirstOrDefault();
        if (lv is null) { Console.WriteLine("[pp] 没有 LocalVariable"); return; }

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        ExpressionSerializer.WriteExpression(lv, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[pp] EX_LocalVariable 写出 {bytes.Length} 字节: {BitConverter.ToString(bytes)}");

        var v = lv.Variable;
        Console.WriteLine($"[pp] Old={(v.Old is null ? "null" : v.Old.Index.ToString())}  New={(v.New is null ? "null" : "有")}");
        Console.WriteLine($"[pp] ShouldSerializeOld={v.ShouldSerializeOld()}  ShouldSerializeNew={v.ShouldSerializeNew()}");

        if (v.New is not null)
        {
            foreach (var f in v.New.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = f.GetValue(v.New);
                var s = val?.ToString() ?? "null";
                if (s.Length > 60) s = s[..60] + "...";
                Console.WriteLine($"     FFieldPath.{f.Name} = {s}");
            }
        }
    }
}
