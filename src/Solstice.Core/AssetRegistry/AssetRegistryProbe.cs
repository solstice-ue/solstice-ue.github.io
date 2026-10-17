using UAssetRegistry;

namespace Solstice.Core.AssetRegistry;

/// <summary>
/// 用 submodule 里的 UAssetRegistry 做一次完整解析，只回答一个问题：
/// <b>这份文件里到底有多少个资产</b>。
///
/// <para>
/// 这是 Phase 0 的探针，不是产品代码：它不暴露 <c>FAssetRegistryState</c>，
/// 以免上层过早依赖一个尚未在 WASM 下验证过的类型。
/// </para>
///
/// <para>
/// 已知实测：KARDS 样本 6.2 MB / version 21 → 16250 个资产，桌面端 1.4 秒。
/// 依赖节点与包数据均为 0，说明该样本不含依赖图与包元数据。
/// </para>
/// </summary>
public static class AssetRegistryProbe
{
    /// <summary>尝试完整解析并返回资产条数。失败时给出可读原因，不抛异常。</summary>
    public static bool TryCountAssets(byte[] data, out int assetCount, out string? error)
    {
        assetCount = 0;
        error = null;

        try
        {
            var state = AssetRegistryLoader.LoadFromBytes(data);
            assetCount = state.PreallocatedAssetDataBuffers.Count();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }
}
