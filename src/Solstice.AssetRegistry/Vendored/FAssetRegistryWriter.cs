using System.Text;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

namespace UAssetRegistry;

/// <summary>
/// 语义级 AssetRegistry.bin 写入器。
/// 镜像 FAssetRegistryReader / FStore / LoadNameBatch 的序列化顺序与文件布局，
/// 底层写入使用 UAssetAPI 的 UnrealBinaryWriter。
/// 说明：tag 值统一按 AnsiString 类型重建全局 FStore（语义等价，字节与原文件不同）。
///
/// 文件布局（version &gt;= FixedTags）：
///   header(GUID+version+bool) → name batch → 全局 FStore → FAssetData[] → dependencies → packages
/// 每个 FAssetData 的 map 头(ulong) 指向全局 FStore.Pairs 中的一个连续段。
/// </summary>
public class FAssetRegistryWriter
{
    private const uint BeginMagic = 0x12345679u;  // _BEGIN_MAGIC (TextFirst load order)
    private const uint EndMagic = 0x87654321u;

    private readonly UnrealBinaryWriter _w;
    private readonly FAssetRegistryState _state;

    private readonly List<string> _names = new();
    private readonly Dictionary<string, int> _nameIndex = new();

    // 全局 tag 存储
    private readonly List<(FName key, string value)> _allPairs = new();
    private readonly Dictionary<string, int> _ansiIndex = new();
    private readonly List<string> _ansiValues = new();
    private uint[] _ansiOffsets = Array.Empty<uint>();
    private byte[] _ansiBytes = Array.Empty<byte>();
    private readonly List<(int begin, int count)> _assetRanges = new();

    public FAssetRegistryWriter(Stream stream, FAssetRegistryState state)
    {
        _w = new UnrealBinaryWriter(stream);
        _state = state;
    }

    public void Write()
    {
        CollectNames();
        CollectTagStore();
        WriteHeader();
        WriteNameBatch();
        WriteFStore();
        WriteAssetDataArray();
        WriteDependenciesAndPackages();
        _w.Flush();
    }

    // ---- 收集 ----

    /// <summary>收集所有 FName 字符串，构建自洽的 name 表。</summary>
    private void CollectNames()
    {
        AddName("None");
        foreach (var a in _state.PreallocatedAssetDataBuffers)
        {
            AddName(a.PackagePath.Value?.Value);
            AddName(a.AssetClass.Value?.Value);
            AddName(a.ClassPackage.Value?.Value);
            AddName(a.PackageName.Value?.Value);
            AddName(a.AssetName.Value?.Value);

            foreach (var kv in a.TagsAndValues)
            {
                AddName(kv.Key.Value?.Value);
            }

            if (a.TaggedAssetBundles is not null)
            {
                foreach (var b in a.TaggedAssetBundles.Bundles)
                {
                    AddName(b.BundleName.Value?.Value);
                    foreach (var so in b.BundleAssets)
                    {
                        var (pkg, asset) = SplitPath(so.AssetPathName);
                        AddName(pkg);
                        AddName(asset);
                    }
                }
            }
        }
    }

    /// <summary>收集所有资产的 tag pairs，构建全局 FStore 内容（AnsiString 去重表 + 每个资产的 pair 段）。</summary>
    private void CollectTagStore()
    {
        foreach (var a in _state.PreallocatedAssetDataBuffers)
        {
            var begin = _allPairs.Count;
            foreach (var kv in a.TagsAndValues)
            {
                var value = kv.Value ?? string.Empty;
                _allPairs.Add((kv.Key, value));

                if (!_ansiIndex.ContainsKey(value))
                {
                    _ansiIndex[value] = _ansiValues.Count;
                    _ansiValues.Add(value);
                }
            }
            _assetRanges.Add((begin, _allPairs.Count - begin));
        }

        // 计算 AnsiString 偏移 + 拼接字节
        _ansiOffsets = new uint[_ansiValues.Count];
        using var blob = new MemoryStream();
        for (var i = 0; i < _ansiValues.Count; i++)
        {
            _ansiOffsets[i] = (uint)blob.Length;
            var bytes = Encoding.UTF8.GetBytes(_ansiValues[i]);
            blob.Write(bytes, 0, bytes.Length);
            blob.WriteByte(0); // \0 结尾
        }
        _ansiBytes = blob.ToArray();
    }

    private void AddName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        if (!_nameIndex.ContainsKey(name))
        {
            _nameIndex[name] = _names.Count;
            _names.Add(name);
        }
    }

    // ---- header ----

    private void WriteHeader()
    {
        // FAssetRegistryVersion.GUID (0x717F9EE7-E9B0493A-88B39132-1B388107) + int32 version
        _w.Write(0x717F9EE7u);
        _w.Write(0xE9B0493Au);
        _w.Write(0x88B39132u);
        _w.Write(0x1B388107u);
        _w.Write((int)_state.Version);

        if (_state.Version >= FAssetRegistryVersionType.AddedHeader)
            _w.Write(_state.FilterEditorOnly ? 1 : 0); // 4 字节 int bool（与 ReadBooleanInt 对应）
    }

    // ---- name batch ----

    private void WriteNameBatch()
    {
        var num = _names.Count;
        _w.Write(num);
        _w.Write((uint)0);              // numStringBytes（读取时忽略）
        _w.Write((ulong)0);             // hashVersion（读取时忽略）
        for (var i = 0; i < num; i++) _w.Write((ulong)0); // hashes（读取时忽略）

        // FSerializedNameHeader（2 字节：IsUtf16 + 15 bit 长度），全 UTF8
        foreach (var name in _names)
        {
            var len = Encoding.UTF8.GetByteCount(name);
            if (len > 0x7FFF) throw new InvalidOperationException($"Name too long: {name}");
            _w.Write((byte)((len >> 8) & 0x7F));
            _w.Write((byte)(len & 0xFF));
        }

        // name 数据（无 \0，与 LoadNameBatch 对应）
        foreach (var name in _names)
        {
            _w.Write(Encoding.UTF8.GetBytes(name));
        }
    }

    // ---- 全局 FStore ----

    private void WriteFStore()
    {
        _w.Write(BeginMagic);

        // nums[11]: NumberlessNames=0, Names=0, NumlessExportPaths=0, ExportPaths=0, Texts=0,
        //           AnsiOffsets=n, WideOffsets=0, AnsiBytes=len, WideBytes=0,
        //           NumberlessPairs=0, Pairs=_allPairs.Count
        _w.Write(0); _w.Write(0); _w.Write(0); _w.Write(0); _w.Write(0);
        _w.Write(_ansiOffsets.Length);
        _w.Write(0);
        _w.Write(_ansiBytes.Length);
        _w.Write(0);
        _w.Write(0);
        _w.Write(_allPairs.Count);

        // TextFirst 顺序：
        _w.Write((uint)0);               // 4 字节占位（Texts 前）
        // Texts: 0 个
        // NumberlessNames: 0 个
        // Names: 0 个
        // NumberlessExportPaths: 0 个
        // ExportPaths: 0 个
        foreach (var o in _ansiOffsets) _w.Write(o);  // AnsiStringOffsets
        // WideStringOffsets: 0 个
        _w.Write(_ansiBytes);                         // AnsiStrings
        // WideStrings: 0 个
        // NumberlessPairs: 0 个
        foreach (var (key, value) in _allPairs)       // Pairs
        {
            WriteFName(key);
            var id = ((uint)_ansiIndex[value] << 3) | (uint)EValueType.AnsiString;
            _w.Write(id);
        }
        _w.Write(EndMagic);
    }

    // ---- 资产数据 ----

    private void WriteAssetDataArray()
    {
        var assets = _state.PreallocatedAssetDataBuffers;
        _w.Write(assets.Length);
        for (var i = 0; i < assets.Length; i++)
        {
            WriteAssetData(assets[i], _assetRanges[i]);
        }
    }

    private void WriteAssetData(FAssetData a, (int begin, int count) range)
    {
        // version >= RemoveAssetPathFNames：不再写 oldObjectPath
        WriteFName(a.PackagePath);

        if (_state.Version >= FAssetRegistryVersionType.ClassPaths)
        {
            // FTopLevelAssetPath = (ClassPackage, AssetClass)。
            // 上游把 ClassPackage 硬写成 "None"，写回后所有资产的类路径都废了。
            WriteFName(a.ClassPackage);
            WriteFName(a.AssetClass);
        }
        else
        {
            WriteFName(a.AssetClass);
        }

        // version >= RemovedMD5Hash：不写 oldGroupNames
        WriteFName(a.PackageName);
        WriteFName(a.AssetName);

        if (_state.Version >= FAssetRegistryVersionType.RemoveAssetPathFNames && !_state.FilterEditorOnly)
            WriteFNameStr("None"); // OptionalOuterPath

        // map 头：num 在高 32 位，pairBegin 在低 32 位（无 numberless key）
        _w.Write(((ulong)(uint)range.count << 32) | (uint)range.begin);

        WriteBundles(a.TaggedAssetBundles);
        WriteChunkIDs(a);

        if (_state.Ver >= ObjectVersion.VER_UE4_COOKED_ASSETS_IN_EDITOR_SUPPORT)
            _w.Write((uint)a.PackageFlags);
    }

    private void WriteBundles(FAssetBundleData? bundles)
    {
        if (bundles is null)
        {
            _w.Write(0);
            return;
        }

        _w.Write(bundles.Bundles.Length);
        foreach (var b in bundles.Bundles)
        {
            WriteFName(b.BundleName);
            _w.Write(b.BundleAssets.Length);
            foreach (var so in b.BundleAssets)
            {
                var (pkg, asset) = SplitPath(so.AssetPathName);
                if (_state.Version >= FAssetRegistryVersionType.ClassPaths)
                {
                    WriteFNameStr(pkg);
                    WriteFNameStr(asset);
                }
                else
                {
                    WriteFNameStr(so.AssetPathName);
                }
                _w.Write(new FString(so.SubPathString));
            }
        }
    }

    private void WriteChunkIDs(FAssetData a)
    {
        if (_state.Ver >= ObjectVersion.VER_UE4_CHANGED_CHUNKID_TO_BE_AN_ARRAY_OF_CHUNKIDS)
        {
            _w.Write(a.ChunkIDs.Length);
            foreach (var c in a.ChunkIDs) _w.Write(c);
        }
        else if (_state.Ver >= ObjectVersion.VER_UE4_ADDED_CHUNKID_TO_ASSETDATA_AND_UPACKAGE)
        {
            _w.Write(a.ChunkIDs.Length > 0 ? a.ChunkIDs[0] : 0);
        }
    }

    // ---- 依赖节点 / 包数据（语义级：为空时写空）----

    private void WriteDependenciesAndPackages()
    {
        if (_state.Version < FAssetRegistryVersionType.RemovedMD5Hash)
            return;

        if (_state.Version < FAssetRegistryVersionType.AddedDependencyFlags)
        {
            _w.Write(_state.PreallocatedDependsNodeDataBuffers.Length);
        }
        else
        {
            _w.Write((long)4); // dependencySectionSize：仅含节点计数的 4 字节
            _w.Write(_state.PreallocatedDependsNodeDataBuffers.Length);
        }

        _w.Write(_state.PreallocatedPackageDataBuffers.Length);
    }

    // ---- 工具 ----

    /// <summary>写 FName（带 number 后缀，镜像 FAssetRegistryReader.ReadFName 的格式）。</summary>
    private void WriteFName(FName name)
        => WriteFNameStr(name.Value?.Value, name.Number);

    private void WriteFNameStr(string? name, int number = 0)
    {
        if (!_nameIndex.TryGetValue(name ?? string.Empty, out var index))
        {
            throw new InvalidOperationException($"Name '{name}' was not collected before writing");
        }

        if (number > 0)
        {
            _w.Write((uint)index | 0x80000000u);
            _w.Write((uint)number);
        }
        else
        {
            _w.Write((uint)index);
        }
    }

    private static (string pkg, string asset) SplitPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return ("None", path ?? string.Empty);
        var dot = path.LastIndexOf('.');
        return dot >= 0 ? (path[..dot], path[(dot + 1)..]) : ("None", path);
    }
}
