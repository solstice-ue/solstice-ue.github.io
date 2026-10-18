using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace Solstice.Core.Kismet;

/// <summary>
/// 从资产的 Import 表建立「函数名 → FPackageIndex」索引。
///
/// <para>
/// EX_FinalFunction 的 StackNode 是 FPackageIndex：正数指向本包 Export，
/// 负数指向 Import 表。实测 card_brawl_test1 有 48 条 Import，
/// 其中 ClassName=Function 的条目形如 IsValid(-11) / GetChooseSpawnCards(-12)。
/// </para>
///
/// <para>
/// 生成时复用现有 Import 即可，不必新增——函数集合来自目标资产本身。
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
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // Import 的索引是 1 基负数：第 1 条 = -1
            index.TryAdd(name, new FPackageIndex(-(i + 1)));
        }

        return index;
    }
}
