using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class PointerNewFormTests
{
    [Fact]
    public void 构造New形态属性指针()
    {
        if (!TestAssets.Available) { Console.WriteLine("[pn] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var name = new FName(asset, "EntryPoint");
        var path = new FFieldPath { Path = [name], ResolvedOwner = new FPackageIndex(3) };
        var pointer = new KismetPropertyPointer(path);
        var expr = new EX_LocalVariable { Variable = pointer };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        ExpressionSerializer.WriteExpression(expr, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[pn] {bytes.Length} 字节: {BitConverter.ToString(bytes)}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = (EX_LocalVariable)ExpressionSerializer.ReadExpression(rd);
        Console.WriteLine($"[pn] 读回 {back.Inst}  Path={string.Join(".", back.Variable.New.Path.Select(x => x.ToString()))}  owner={back.Variable.New.ResolvedOwner.Index}");
    }
}
