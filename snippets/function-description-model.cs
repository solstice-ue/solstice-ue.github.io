// 目的：为 UKAPI 的「生成路径」定义高层输入模型（函数名、签名、局部变量、语句）。
//
// 假设与受支持范围：仅覆盖 CallStatement / IfStatement / ReturnStatement 三种语句，
// 以及常量、局部变量、嵌套调用三种参数。不支持循环、作用域分析、类型推导、多返回值。
//
// 依赖：无。本文件刻意不引用 UAssetAPI，以便在纯领域层讨论。
//
// 验证状态：PROPOSED（提议）。未编译、未接入任何序列化器、无测试。
//
// 明确不实现：表达式对象构造、Import 构造、FPackageIndex 绑定、跳转偏移计算、序列化。
// 这些属于编译器的职责，见 docs/bytecode-generation.md。
//
// 仅为概念草图 —— 尚未接入 UAssetAPI。
// 这里建模的是高层意图。必须由一个独立的编译器把它翻译成
// 实际的表达式对象，并使用已针对所选 UAssetAPI 版本验证过的 API。

namespace Solstice.Snippets;

public sealed record FunctionDescription(
    string FunctionName,
    FunctionSignature Signature,
    IReadOnlyList<LocalVariableDeclaration> Locals,
    IReadOnlyList<Statement> Statements);

public sealed record FunctionSignature(
    string OwnerType,
    IReadOnlyList<ParameterSignature> Parameters,
    string? ReturnType);

public sealed record ParameterSignature(string Name, string TypeName);

public sealed record LocalVariableDeclaration(string Name, string TypeName);

public abstract record Statement;

public sealed record CallStatement(
    FunctionReference Function,
    IReadOnlyList<Argument> Arguments) : Statement;

public sealed record IfStatement(
    Argument Condition,
    IReadOnlyList<Statement> Then,
    IReadOnlyList<Statement> Else) : Statement;

public sealed record ReturnStatement(Argument? Value) : Statement;

public abstract record Argument;

public sealed record ConstantArgument(object? Value) : Argument;

public sealed record LocalVariableArgument(string Name) : Argument;

public sealed record CallArgument(
    FunctionReference Function,
    IReadOnlyList<Argument> Arguments) : Argument;

public sealed record FunctionReference(
    string PackagePath,
    string OwnerType,
    string FunctionName,
    string? SignatureFingerprint = null);
