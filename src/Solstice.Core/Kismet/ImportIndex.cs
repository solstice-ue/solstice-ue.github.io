using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace Solstice.Core.Kismet;

/// <summary>
/// 从资产的 Import 表建立「函数名 → FPackageIndex」索引，并按需新增条目。
///
/// <para>
/// EX_FinalFunction 的 StackNode 是 FPackageIndex：正数指向本包 Export，
/// 负数指向 Import 表。实测 card_brawl_test1 有 48 条 Import，
/// 其中 ClassName=Function 的条目形如 IsValid(-11) / GetChooseSpawnCards(-12)。
/// </para>
///
/// <para>
/// 只能复用资产已有的符号名：<c>new FName(asset, name)</c> 会给缺失的名字分配新下标，
/// 因此新增 Import 时函数名要么已在本包名字表里，要么接受名字表被扩展。
/// </para>
/// </summary>
public static class ImportIndex
{
    public static IReadOnlyDictionary<string, FPackageIndex> BuildFunctionIndex(UAsset asset)
    {
        var index = new Dictionary<string, FPackageIndex>(StringComparer.Ordinal);

        for (var i = 0; i < asset.Imports.Count; i++)
        {
            var im = asset.Imports[i];
            if (im.ClassName?.ToString() != "Function")
            {
                continue;
            }

            var name = im.ObjectName?.ToString();
            if (string.IsNullOrEmpty(name) || name == "Default__Function")
            {
                continue;
            }

            // Import 的索引是 1 基负数：第 1 条 = -1
            index.TryAdd(name, new FPackageIndex(-(i + 1)));
        }

        return index;
    }

    /// <summary>
    /// 保证指定函数在 Import 表里有条目，返回它的 FPackageIndex。
    ///
    /// <para>
    /// 已存在则直接返回；不存在时**克隆**一个现有函数导入（沿用它的
    /// ClassPackage / ClassName / OuterIndex / PackageName），只替换 ObjectName。
    /// 这样不需要猜测 OuterIndex 链，也不会跨资产复用索引。
    /// </para>
    /// </summary>
    public static FPackageIndex EnsureFunction(UAsset asset, string functionName)
    {
        var existing = BuildFunctionIndex(asset);
        if (existing.TryGetValue(functionName, out var found))
        {
            return found;
        }

        var template = asset.Imports.FirstOrDefault(im =>
            im.ClassName?.ToString() == "Function" &&
            im.ObjectName?.ToString() is { Length: > 0 } n &&
            n != "Default__Function");

        if (template is null)
        {
            throw new InvalidOperationException(
                "资产里没有可作模板的函数导入，无法新增 Import 条目");
        }

        var clone = new Import(
            template.ClassPackage,
            template.ClassName,
            template.OuterIndex,
            new FName(asset, functionName),
            template.bImportOptional)
        {
            PackageName = template.PackageName
        };

        asset.Imports.Add(clone);
        return new FPackageIndex(-asset.Imports.Count);
    }
}
