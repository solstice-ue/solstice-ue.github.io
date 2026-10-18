using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Tests;

/// <summary>
/// 生成闭环：用代码构造表达式 → 序列化 → 用 UAssetAPI 重新解析 → 结构一致。
/// </summary>
public class BytecodeGenerationTests
{
    [Fact]
    public void 构造表达式写出再解析()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));

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
        Console.WriteLine($"[gen] 生成 {generated.Length} 条表达式，{bytes.Length} 字节");
        Console.WriteLine($"[gen] 字节: {BitConverter.ToString(bytes)}");

        var rs = new MemoryStream(bytes);
        var rd = new AssetBinaryReader(rs, asset);
        var back = new List<KismetExpression>();
        while (rs.Position < rs.Length)
        {
            back.Add(ExpressionSerializer.ReadExpression(rd));
        }

        Console.WriteLine($"[gen] 读回 {back.Count} 条: {string.Join(" -> ", back.Select(x => x.Inst))}");
        Assert.Equal(2, back.Count);
        Assert.Equal("Return", back[0].Inst);
        Assert.Equal("EndOfScript", back[1].Inst);

        var ret = (EX_Return)back[0];
        Assert.Equal("IntConst", ret.ReturnExpression.Inst);
        Assert.Equal(42, ((EX_IntConst)ret.ReturnExpression).Value);
        Console.WriteLine("[gen] 语义校验通过：return 42");
    }
    [Fact]
    public void 跳转指令的字节布局()
    {
        var single = Environment.GetEnvironmentVariable("SOLSTICE_BP_FILE")!;
        var usmap = Environment.GetEnvironmentVariable("SOLSTICE_USMAP")!;
        var asset = new UAsset(single, EngineVersion.VER_UE5_6, new Usmap(usmap));

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

        Console.WriteLine($"[jmp] 读回: {string.Join(" -> ", back.Select(x => x.Inst))}");
        var jn = (EX_JumpIfNot)back[0];
        Console.WriteLine($"[jmp] JumpIfNot.CodeOffset={jn.CodeOffset} 条件={jn.BooleanExpression.Inst}");
        var j = (EX_Jump)back[1];
        Console.WriteLine($"[jmp] Jump.CodeOffset={j.CodeOffset}");
        Assert.Equal(16u, jn.CodeOffset);
        Assert.Equal(32u, j.CodeOffset);
    }}
