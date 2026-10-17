using UAssetAPI.UnrealTypes;
using UAssetRegistry;

namespace Solstice.Core.AssetRegistry;

/// <summary>列表里的一行。只保留展示与筛选需要的字段。</summary>
public sealed record AssetEntry(
    string ObjectPath,
    string PackagePath,
    string AssetName,
    string AssetClass);

/// <summary>
/// 一份可编辑的 AssetRegistry 文档。
///
/// <para>
/// 负责：解析字节 → 暴露列表 → 新增条目 → 写回字节。
/// 不负责：UI 状态、文件读写、差异比较。
/// </para>
///
/// <para>
/// 写回是**语义级**的：输出合法、读回一致，但字节与原文件不同
/// （name 表与 FStore 会被重建）。见 docs/glossary.md。
/// </para>
/// </summary>
public sealed class AssetRegistryDocument
{
    private readonly FAssetRegistryState _state;

    private AssetRegistryDocument(FAssetRegistryState state) => _state = state;

    /// <summary>资产条数。</summary>
    public int Count => _state.PreallocatedAssetDataBuffers.Length;

    /// <summary>解析字节。失败抛异常，由调用方转成可读诊断。</summary>
    public static AssetRegistryDocument Load(byte[] data) =>
        new(AssetRegistryLoader.LoadFromBytes(data));

    /// <summary>取出全部条目。16250 条量级，一次性物化可接受。</summary>
    public IReadOnlyList<AssetEntry> GetEntries()
    {
        var buffer = _state.PreallocatedAssetDataBuffers;
        var result = new AssetEntry[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            var a = buffer[i];
            result[i] = new AssetEntry(
                a.ObjectPath,
                a.PackagePath.ToString(),
                a.AssetName.ToString(),
                a.AssetClass.ToString());
        }

        return result;
    }

    /// <summary>
    /// 新增一条卡牌资产。已存在同 ObjectPath 时返回 false，不重复添加。
    /// 命名约定：PackagePath/AssetName.AssetName。
    /// </summary>
    public bool AddAsset(
        string packagePath,
        string assetName,
        string assetClass,
        IDictionary<string, string>? tags = null,
        int chunkId = 0)
    {
        var packageName = $"{packagePath.TrimEnd('/')}/{assetName}";
        var objectPath = $"{packageName}.{assetName}";

        foreach (var existing in _state.PreallocatedAssetDataBuffers)
        {
            if (existing.ObjectPath == objectPath)
            {
                return false;
            }
        }

        _state.AddAsset(new FAssetData(
            packageName: packageName,
            packagePath: packagePath.TrimEnd('/'),
            assetName: assetName,
            assetClass: assetClass,
            tagsAndValues: tags,
            chunkIDs: new[] { chunkId },
            packageFlags: EPackageFlags.PKG_Cooked));

        return true;
    }

    /// <summary>语义级写回。输出是合法的 AssetRegistry.bin，但字节与原文件不同。</summary>
    public byte[] Save() => AssetRegistryLoader.SaveToBytes(_state);
}
