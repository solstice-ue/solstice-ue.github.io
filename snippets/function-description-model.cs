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
