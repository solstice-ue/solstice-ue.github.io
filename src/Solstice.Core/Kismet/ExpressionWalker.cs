using System.Reflection;
using UAssetAPI.Kismet.Bytecode;
using UAssetAPI.Kismet.Bytecode.Expressions;

namespace Solstice.Core.Kismet;

/// <summary>
/// 递归遍历表达式树。
///
/// <para>
/// Kismet 的表达式是**嵌套结构**：EX_Context 的 ObjectExpression / ContextExpression、
/// EX_JumpIfNot 的 BooleanExpression、EX_Return 的 ReturnExpression 都内嵌子表达式。
/// 只看顶层数组会漏掉大量内容（实测 ExecuteUbergraph 顶层没有任何 EX_VariableBase）。
/// </para>
/// </summary>
public static class ExpressionWalker
{
    public static IEnumerable<KismetExpression> Walk(IEnumerable<KismetExpression> roots)
    {
        foreach (var root in roots)
        {
            foreach (var e in Walk(root))
            {
                yield return e;
            }
        }
    }

    public static IEnumerable<KismetExpression> Walk(KismetExpression root)
    {
        if (root is null)
        {
            yield break;
        }

        yield return root;

        foreach (var field in root.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(KismetExpression))
            {
                if (field.GetValue(root) is KismetExpression child)
                {
                    foreach (var e in Walk(child))
                    {
                        yield return e;
                    }
                }
            }
            else if (field.FieldType == typeof(KismetExpression[]))
            {
                if (field.GetValue(root) is KismetExpression[] children)
                {
                    foreach (var c in children)
                    {
                        foreach (var e in Walk(c))
                        {
                            yield return e;
                        }
                    }
                }
            }
        }
    }

    /// <summary>收集所有变量表达式（LocalVariable / InstanceVariable 等）。</summary>
    public static IReadOnlyList<EX_VariableBase> CollectVariables(IEnumerable<KismetExpression> roots)
        => Walk(roots).OfType<EX_VariableBase>().ToArray();
}
