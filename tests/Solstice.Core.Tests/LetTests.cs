using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

public class LetTests
{
    [Fact]
    public void 构造EX_Let并往返()
    {
        if (!TestAssets.Available) { Console.WriteLine("[let] 跳过"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var ptr = new KismetPropertyPointer(new FFieldPath
        {
            Path = [new FName(asset, "EntryPoint")],
            ResolvedOwner = new FPackageIndex(3)
        });

        var let = new EX_Let
        {
            Value = ptr,
            Variable = new EX_LocalVariable { Variable = ptr },
            Expression = new EX_IntConst { Value = 5 }
        };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        ExpressionSerializer.WriteExpression(let, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[let] {bytes.Length} 字节: {BitConverter.ToString(bytes)}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = (EX_Let)ExpressionSerializer.ReadExpression(rd);
        Console.WriteLine($"[let] 读回 {back.Inst}  Variable={back.Variable.Inst}  Expression={back.Expression.Inst}");
        Console.WriteLine($"[let] 值 = {((EX_IntConst)back.Expression).Value}");
    }
}
