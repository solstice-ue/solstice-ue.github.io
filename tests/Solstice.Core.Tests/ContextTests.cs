using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class ContextTests
{
    [Fact]
    public void 构造EX_Context并往返()
    {
        if (!TestAssets.Available) { Console.WriteLine("[cx] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var ptr = new KismetPropertyPointer(new FFieldPath
        {
            Path = [new FName(asset, "EntryPoint")],
            ResolvedOwner = new FPackageIndex(3)
        });

        var inner = new EX_FinalFunction { StackNode = new FPackageIndex(-19), Parameters = [] };
        var ctx = new EX_Context
        {
            ObjectExpression = new EX_LocalVariable { Variable = ptr },
            ContextExpression = inner,
            RValuePointer = ptr,
            Offset = inner.GetSize(asset)
        };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        ExpressionSerializer.WriteExpression(ctx, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[cx] {bytes.Length} 字节: {BitConverter.ToString(bytes.Take(20).ToArray())}...");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = (EX_Context)ExpressionSerializer.ReadExpression(rd);
        Console.WriteLine($"[cx] 读回 {back.Inst}  obj={back.ObjectExpression.Inst}  ctx={back.ContextExpression.Inst}  Offset={back.Offset}");
        Assert.Equal(back.Offset, back.ContextExpression.GetSize(asset));
    }
}
