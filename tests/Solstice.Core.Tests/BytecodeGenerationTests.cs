using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

/// <summary>
/// 生成闭环：构造表达式 → 序列化 → 用 UAssetAPI 重新解析 → 结构一致。
/// 每个测试都用 TestAssets 的独立副本，避免 usmap 句柄互锁。
/// </summary>
public class BytecodeGenerationTests
{
    [Fact]
    public void 构造表达式写出再解析()
    {
        if (!TestAssets.Available) { Console.WriteLine("[gen] 跳过：无样本"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        // 手写一个最小函数体：return 42;
        var generated = new KismetExpression[]
        {
            new EX_Return { ReturnExpression = new EX_IntConst { Value = 42 } },
            new EX_EndOfScript()
        };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        foreach (var e in generated) ExpressionSerializer.WriteExpression(e, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[gen] {generated.Length} 条表达式 -> {bytes.Length} 字节: {BitConverter.ToString(bytes)}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = new List<KismetExpression>();
        while (rs.Position < rs.Length) back.Add(ExpressionSerializer.ReadExpression(rd));

        Console.WriteLine($"[gen] 读回: {string.Join(" -> ", back.Select(x => x.Inst))}");
        Assert.Equal(2, back.Count);
        Assert.Equal("Return", back[0].Inst);
        Assert.Equal(42, ((EX_IntConst)((EX_Return)back[0]).ReturnExpression).Value);
    }

    [Fact]
    public void 跳转指令的字节布局()
    {
        if (!TestAssets.Available) { Console.WriteLine("[jmp] 跳过：无样本"); return; }

        var copy = TestAssets.Prepare();
        var asset = new UAsset(copy.AssetPath, EngineVersion.VER_UE5_6, new Usmap(copy.UsmapPath));

        var gen = new KismetExpression[]
        {
            new EX_JumpIfNot { CodeOffset = 16, BooleanExpression = new EX_IntConst { Value = 1 } },
            new EX_Jump { CodeOffset = 32 },
            new EX_EndOfScript()
        };

        var ms = new MemoryStream();
        var w = new AssetBinaryWriter(ms, asset);
        foreach (var e in gen) ExpressionSerializer.WriteExpression(e, w);
        var bytes = ms.ToArray();
        Console.WriteLine($"[jmp] {bytes.Length} 字节: {BitConverter.ToString(bytes)}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = new List<KismetExpression>();
        while (rs.Position < rs.Length) back.Add(ExpressionSerializer.ReadExpression(rd));

        Assert.Equal(16u, ((EX_JumpIfNot)back[0]).CodeOffset);
        Assert.Equal(32u, ((EX_Jump)back[1]).CodeOffset);
    }
}
