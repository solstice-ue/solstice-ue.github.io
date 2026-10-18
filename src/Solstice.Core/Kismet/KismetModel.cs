namespace Solstice.Core.Kismet;

/// <summary>插销类型。颜色由 UI 按此映射，类型推断也读它。</summary>
public enum PinType
{
    Exec,
    Int,
    Bool,
    Float,
    String,
    Object,
    Struct,
    Enum
}

/// <summary>
/// 用到的操作码及其真实字节值。
/// 取值来源：UAssetAPI 的 <c>EExprToken</c>（atenfyr/UAssetAPI，对应 UE 5.x）。
///
/// <para>注意：UE5 **没有** <c>EX_EndOfScript</c>，那是 UE3 的；函数结束只有 <c>EX_Return</c>。</para>
/// </summary>
public static class KismetOpcode
{
    public const byte LocalVariable = 0x00;
    public const byte InstanceVariable = 0x01;
    public const byte Return = 0x04;
    public const byte Jump = 0x06;
    public const byte JumpIfNot = 0x07;
    public const byte Nothing = 0x0B;
    public const byte Let = 0x0F;
    public const byte EndFunctionParms = 0x16;
    public const byte VirtualFunction = 0x1B;
    public const byte FinalFunction = 0x1C;
    public const byte IntConst = 0x1D;
    public const byte IntZero = 0x25;
    public const byte IntOne = 0x26;
    public const byte True = 0x27;
    public const byte False = 0x28;

    /// <summary>名字 → 字节值。未收录的返回 null（UI 显示为 <c>??</c>，提醒补表）。</summary>
    public static byte? Lookup(string name) => name switch
    {
        "EX_LocalVariable" => LocalVariable,
        "EX_InstanceVariable" => InstanceVariable,
        "EX_Return" => Return,
        "EX_Jump" => Jump,
        "EX_JumpIfNot" => JumpIfNot,
        "EX_Nothing" => Nothing,
        "EX_Let" => Let,
        "EX_EndFunctionParms" => EndFunctionParms,
        "EX_VirtualFunction" => VirtualFunction,
        "EX_FinalFunction" => FinalFunction,
        "EX_IntConst" => IntConst,
        "EX_IntZero" => IntZero,
        "EX_IntOne" => IntOne,
        "EX_True" => True,
        "EX_False" => False,
        _ => null
    };
}

/// <summary>一条编译产物：操作码 + 操作数。</summary>
public sealed record KismetInstruction(string Opcode, string Operand, int? JumpTarget = null)
{
    /// <summary>操作码的真实字节值；未收录为 null。</summary>
    public byte? Token => KismetOpcode.Lookup(Opcode);

    public string TokenHex => Token is { } t ? $"0x{t:X2}" : "??";

    public string Render() => JumpTarget is { } t
        ? $"{Opcode,-24} → {t:000}"
        : string.IsNullOrEmpty(Operand) ? Opcode : $"{Opcode,-24} {Operand}";
}

/// <summary>表达式参数（数据流上游）。</summary>
public abstract record KArg(PinType Type);

public sealed record IntArg(int Value) : KArg(PinType.Int);

public sealed record LocalArg(string Name) : KArg(PinType.Int);

/// <summary>节点（编辑器层的逻辑单元，一行一个）。</summary>
public abstract record KNode(string Title);

public sealed record CallNode(string Function, IReadOnlyList<KArg> Args) : KNode(Function);

public sealed record IfNode(KArg Condition, IReadOnlyList<KNode> Then) : KNode("If");

public sealed record ReturnNode() : KNode("Return");
