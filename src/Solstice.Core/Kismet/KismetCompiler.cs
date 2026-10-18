namespace Solstice.Core.Kismet;

/// <summary>
/// UKAPI 最小编译器：把平铺节点列表编译成 `EX_` 指令序列。
///
/// <para>
/// 这一版只负责**生成指令列表**（供左栏预览），不负责序列化进 uasset。
/// 控制流用「占位标签 + 回填」的方式生成跳转目标，与真实字节码的
/// `EX_Jump` / `EX_JumpIfNot` 偏移语义一致（跳转指向表达式索引）。
/// </para>
/// </summary>
public static class KismetCompiler
{
    private sealed record Raw(string Opcode, string Operand, string? Label, string? TargetLabel);

    public static IReadOnlyList<KismetInstruction> Compile(IReadOnlyList<KNode> nodes)
    {
        var raw = new List<Raw>();
        var labelSeq = 0;

        void Emit(string opcode, string operand = "", string? label = null, string? target = null)
            => raw.Add(new Raw(opcode, operand, label, target));

        void EmitArg(KArg arg)
        {
            switch (arg)
            {
                case IntArg i:
                    Emit("EX_IntConst", i.Value.ToString());
                    break;
                case LocalArg l:
                    Emit("EX_LocalVariable", l.Name);
                    break;
                default:
                    Emit("EX_Nothing", "<unsupported arg>");
                    break;
            }
        }

        void EmitNode(KNode node)
        {
            switch (node)
            {
                case CallNode c:
                    foreach (var a in c.Args)
                    {
                        EmitArg(a);
                    }

                    Emit("EX_FinalFunction", c.Function);
                    Emit("EX_EndFunctionParms");
                    break;

                case IfNode f:
                {
                    var elseLabel = $"L{labelSeq++}";
                    var endLabel = $"L{labelSeq++}";

                    EmitArg(f.Condition);
                    Emit("EX_JumpIfNot", "", null, elseLabel);

                    foreach (var n in f.Then)
                    {
                        EmitNode(n);
                    }

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

        foreach (var n in nodes)
        {
            EmitNode(n);
        }

        // UE5 没有 EX_EndOfScript：函数体末尾就是 EX_Return。

        // 回填标签 → 指令索引
        var labelIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < raw.Count; i++)
        {
            if (raw[i].Label is { } l)
            {
                labelIndex[l] = i;
            }
        }

        var result = new KismetInstruction[raw.Count];
        for (var i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            int? target = null;
            if (r.TargetLabel is { } tl && labelIndex.TryGetValue(tl, out var idx))
            {
                target = idx;
            }

            result[i] = new KismetInstruction(r.Opcode, r.Operand, target);
        }

        return result;
    }
}
