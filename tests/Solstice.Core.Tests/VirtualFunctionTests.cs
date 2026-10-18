using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class VirtualFunctionTests
{
    [Fact]
    public void EX_LocalVirtualFunction的字段与序列化()
    {
        if (!TestAssets.Available) { Console.WriteLine("[vf] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        Console.WriteLine("[vf] 字段:");
        foreach (var f in typeof(EX_LocalVirtualFunction).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            Console.WriteLine($"[vf]   {f.FieldType.Name} {f.Name}");
        }

        // 注意：VirtualFunctionName 是 FName，且必须已存在于资产的名字表里；
        // 随手造一个名字能写出，但读回时会因名字表查不到而失败。
        var fn = new EX_LocalVirtualFunction
        {
            VirtualFunctionName = new FName(asset, "K2_Test"),
            Parameters = []
        };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        ExpressionSerializer.WriteExpression(fn, w);
        var bytes = ms.ToArray();

        Console.WriteLine($"[vf] {bytes.Length} 字节: {BitConverter.ToString(bytes)}");
        Console.WriteLine("[vf] 布局: token(45) + FName(8) + EndFunctionParms(16)");

        Assert.Equal(10, bytes.Length);
        Assert.Equal(0x45, bytes[0]);
        Assert.Equal(0x16, bytes[^1]);
    }
}
