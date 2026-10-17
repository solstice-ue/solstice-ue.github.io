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

    /// <summary>注册表格式版本（KARDS 样本为 21）。</summary>
    public int RegistryVersion => (int)_state.Version;

    /// <summary>
    /// 写回时决定「写不写 PackageFlags / ChunkIDs」的 ObjectVersion。
    /// 若低于 UE4 阈值，资产记录会比原文件短，UE 解析直接错位。
    /// </summary>
    public int ObjectVersion => (int)_state.Ver;

    /// <summary>诊断用：首条资产的 ChunkIDs。</summary>
    public string FirstChunkIds =>
        _state.PreallocatedAssetDataBuffers.Length == 0
            ? "(空)"
            : string.Join(",", _state.PreallocatedAssetDataBuffers[0].ChunkIDs);

    /// <summary>诊断用：首条资产的 PackageFlags。</summary>
    public string FirstPackageFlags =>
        _state.PreallocatedAssetDataBuffers.Length == 0
            ? "(空)"
            : _state.PreallocatedAssetDataBuffers[0].PackageFlags.ToString();

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

    /// <summary>
    /// 新增一张卡牌。**会写两条**：Blueprint 与它的 BlueprintGeneratedClass（`_C`）。
    ///
    /// <para>
    /// 实测依据：KARDS 的 2019 个卡牌 `.uasset` 在注册表里对应 4043 条
    /// （≈ 2019 × 2），每对都是 `X.X`（Blueprint）+ `X.X_C`（BlueprintGeneratedClass）。
    /// 两条的 PackageName 相同，只有 AssetName 差一个 `_C`。
    /// </para>
    /// </summary>
    /// <returns>两条都写入返回 true；任一已存在则一条都不写，返回 false。</returns>
    public bool AddCard(
        string packagePath,
        string assetName,
        IDictionary<string, string>? tags = null,
        int chunkId = 0)
    {
        var dir = packagePath.TrimEnd('/');
        var packageName = $"{dir}/{assetName}";
        var classAssetName = assetName + "_C";

        foreach (var existing in _state.PreallocatedAssetDataBuffers)
        {
            if (existing.ObjectPath == $"{packageName}.{assetName}" ||
                existing.ObjectPath == $"{packageName}.{classAssetName}")
            {
                return false;
            }
        }

        _state.AddAsset(new FAssetData(
            packageName: packageName,
            packagePath: dir,
            assetName: assetName,
            assetClass: "Blueprint",
            tagsAndValues: tags,
            chunkIDs: new[] { chunkId },
            packageFlags: EPackageFlags.PKG_Cooked));

        _state.AddAsset(new FAssetData(
            packageName: packageName,
            packagePath: dir,
            assetName: classAssetName,
            assetClass: "BlueprintGeneratedClass",
            tagsAndValues: null,
            chunkIDs: new[] { chunkId },
            packageFlags: EPackageFlags.PKG_Cooked));

        return true;
    }

    /// <summary>
    /// 写回。上游写入器会把 name batch 的哈希表写成 0，这里补算回来
    /// （否则 UE 查不到名字，bin 在游戏里读不了）。
    /// </summary>
    public byte[] Save()
    {
        var bytes = AssetRegistryLoader.SaveToBytes(_state);

        var rebuilt = Solstice.AssetRegistry.NameBatchRebuilder.Rebuild(bytes, out var error);
        if (rebuilt is null)
        {
            throw new InvalidOperationException($"写回后无法修复 name batch 哈希：{error}");
        }

        return rebuilt;
    }

    /// <summary>
    /// 逐条快照，用于写回前后比对。包含 tags、ChunkIDs 与 PackageFlags，
    /// 任何一项丢失都说明写回不是「语义级等价」。
    /// </summary>
    public IReadOnlyList<string> Snapshot()
    {
        var buffer = _state.PreallocatedAssetDataBuffers;
        var result = new string[buffer.Length];

        for (var i = 0; i < buffer.Length; i++)
        {
            var a = buffer[i];
            var tags = string.Join(
                "|",
                a.TagsAndValues
                    .Select(kv => $"{kv.Key}={kv.Value}")
                    .OrderBy(s => s, StringComparer.Ordinal));

            result[i] = string.Join(
                "\u0001",
                a.ObjectPath,
                a.AssetClass.ToString(),
                string.Join(",", a.ChunkIDs),
                a.PackageFlags.ToString(),
                tags);
        }

        return result;
    }
}
