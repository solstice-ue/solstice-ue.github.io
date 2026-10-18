using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;

namespace Solstice.Core.Tests;

public class SignatureProbeTests
{
    [Fact]
    public void FunctionExport是否带参数信息()
    {
        if (!TestAssets.Available) { Console.WriteLine("[sig] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var fn = asset.Exports.OfType<FunctionExport>().First();
        Console.WriteLine($"[sig] 样本函数: {fn.ObjectName}");
        Console.WriteLine("[sig] FunctionExport 的字段/属性:");
        foreach (var f in typeof(FunctionExport).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"    field {f.FieldType.Name} {f.Name}");
        }
        foreach (var p in typeof(FunctionExport).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"    prop  {p.PropertyType.Name} {p.Name}");
        }

        // 看字节码开头是否有参数读取（EX_LocalVariable 序列）
        var bc = fn.ScriptBytecode!;
        Console.WriteLine($"[sig] 字节码前 6 条: {string.Join(" -> ", bc.Take(6).Select(x => x.Inst))}");
    }
}
