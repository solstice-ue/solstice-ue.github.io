using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Kismet;

/// <summary>
/// UKAPI 最小编译器：平铺节点 → EX_ 指令序列。
///
/// <para>
/// <b>跳转用字节偏移，不是表达式下标。</b>实测 card_brawl_test1 的 ExecuteUbergraph：
/// JumpIfNot 的 CodeOffset 是 199 / 619，Jump 的是 1433，而函数只有 56 条表达式。
/// 所以这里先按占位偏移生成，再用每条指令的编码长度累加回填。
/// </para>
///
/// <para>
/// 长度表来自 UAssetAPI 的 <c>KismetExpression.GetSize</c>：Jump=5（1 opcode + 4 偏移）。
/// </para>
/// </summary>
public static class KismetCompiler
{
    private sealed class Raw
    {
        public string Opcode = "";
        public string Operand = "";
        public string? Label;
        public string? TargetLabel;
        public int Size = 1;
    }

    /// <summary>各操作码的编码字节数（不含跳转偏移以外的可变部分）。</summary>
    private static int SizeOf(string opcode) => opcode switch
    {
        "EX_LocalVariable" or "EX_InstanceVariable" => 9,  // opcode + FKismetPropertyPointer(8)
        "EX_IntConst" => 5,                                 // opcode + int32
        "EX_FinalFunction" or "EX_VirtualFunction" => 5,    // opcode + int32 package index
        "EX_Jump" => 5,                                     // opcode + int32 偏移
        "EX_JumpIfNot" => 5,
        _ => 1                                              // Nothing / Return / EndOfScript / EndFunctionParms
    };

    public static IReadOnlyList<KismetInstruction> Compile(IReadOnlyList<KNode> nodes)
    {
        var raw = new List<Raw>();
        var labelSeq = 0;

        void Emit(string opcode, string operand = "", string? label = null, string? target = null)
            => raw.Add(new Raw { Opcode = opcode, Operand = operand, Label = label, TargetLabel = target, Size = SizeOf(opcode) });

        void EmitArg(KArg arg)
        {
            switch (arg)
            {
                case IntArg i: Emit("EX_IntConst", i.Value.ToString()); break;
                case LocalArg l: Emit("EX_LocalVariable", l.Name); break;
                default: Emit("EX_Nothing", "<unsupported>"); break;
            }
        }

        void EmitNode(KNode node)
        {
            switch (node)
            {
                case CallNode c:
                    foreach (var a in c.Args) EmitArg(a);
                    Emit("EX_FinalFunction", c.Function);
                    Emit("EX_EndFunctionParms");
                    break;

                case IfNode f:
                {
                    var elseLabel = $"L{labelSeq++}";
                    var endLabel = $"L{labelSeq++}";
                    EmitArg(f.Condition);
                    Emit("EX_JumpIfNot", "", null, elseLabel);
                    foreach (var n in f.Then) EmitNode(n);
                    Emit("EX_Jump", "", null, endLabel);
                    Emit("EX_Nothing", "", elseLabel);
                    Emit("EX_Nothing", "", endLabel);
                    break;
                }

                case ReturnNode:
                    Emit("EX_Return");
                    break;
            }
        }

        foreach (var n in nodes) EmitNode(n);
        Emit("EX_EndOfScript");

        // 第一遍：算出每条指令的起始字节偏移
        var offsets = new int[raw.Count];
        var acc = 0;
        for (var i = 0; i < raw.Count; i++)
        {
            offsets[i] = acc;
            acc += raw[i].Size;
        }

        var labelOffset = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < raw.Count; i++)
            if (raw[i].Label is { } l) labelOffset[l] = offsets[i];

        var result = new KismetInstruction[raw.Count];
        for (var i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            int? target = r.TargetLabel is { } tl && labelOffset.TryGetValue(tl, out var o) ? o : null;
            result[i] = new KismetInstruction(r.Opcode, r.Operand, target);
        }

        return result;
    }

    /// <summary>
    /// 编译成 UAssetAPI 的表达式对象，可直接交给 <c>ExpressionSerializer</c> 序列化。
    ///
    /// <para>
    /// 当前覆盖常量、返回、结束。变量与调用需要属性指针 / FPackageIndex，
    /// 由后续阶段接入（见 <see cref="ExpressionWalker.BuildVariableIndex"/>）。
    /// </para>
    /// </summary>
    public static IReadOnlyList<KismetExpression> CompileToExpressions(
        IReadOnlyList<KNode> nodes,
        IReadOnlyDictionary<string, KismetPropertyPointer>? variables = null)
    {
        var result = new List<KismetExpression>();

        void EmitArg(KArg arg)
        {
            switch (arg)
            {
                case IntArg i:
                    result.Add(new EX_IntConst { Value = i.Value });
                    break;

                case LocalArg l when variables is not null && variables.TryGetValue(l.Name, out var pointer):
                    // 复用现有字节码里的属性指针：FFieldPath 自带变量名与归属索引
                    result.Add(new EX_LocalVariable { Variable = pointer });
                    break;

                default:
                    result.Add(new EX_Nothing());
                    break;
            }
        }

        foreach (var node in nodes)
        {
            switch (node)
            {
                case CallNode c:
                    foreach (var a in c.Args)
                    {
                        EmitArg(a);
                    }

                    result.Add(new EX_Nothing());
                    break;

                case ReturnNode:
                    result.Add(new EX_Return { ReturnExpression = new EX_Nothing() });
                    break;
            }
        }

        result.Add(new EX_EndOfScript());
        return result;
    }
}
