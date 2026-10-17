using UAssetAPI.UnrealTypes;

namespace UAssetRegistry;

/// <summary>单个资产的注册表数据。</summary>
public class FAssetData
{
    public readonly FName PackageName;
    public readonly FName PackagePath;
    public readonly FName AssetName;

    /// <summary>资产类名（FTopLevelAssetPath 的 AssetName 部分）。</summary>
    public readonly FName AssetClass;

    /// <summary>
    /// 资产类所属的包（FTopLevelAssetPath 的 PackageName 部分），例如 <c>/Script/Engine</c>。
    /// 上游版本把这一半丢掉了，导致写回时类包名只能写成 "None"。
    /// </summary>
    public readonly FName ClassPackage;

    public IDictionary<FName, string> TagsAndValues = new Dictionary<FName, string>();
    public FAssetBundleData? TaggedAssetBundles;

    public readonly int[] ChunkIDs;
    public readonly EPackageFlags PackageFlags;

    private static readonly RegistryNameMap DummyMap = new();

    /// <summary>
    /// 手动构造 FAssetData（用于测试 / 程序化建表）。
    /// FName 以 dummy 形式创建（值直接内联，不绑定具体 name map），写回时由 writer 统一收集。
    /// </summary>
    public FAssetData(
        string packageName,
        string packagePath,
        string assetName,
        string assetClass,
        IDictionary<string, string>? tagsAndValues = null,
        int[]? chunkIDs = null,
        EPackageFlags packageFlags = EPackageFlags.PKG_None,
        string classPackage = "None")
    {
        PackageName = FName.DefineDummy(DummyMap, packageName);
        PackagePath = FName.DefineDummy(DummyMap, packagePath);
        AssetName = FName.DefineDummy(DummyMap, assetName);
        AssetClass = FName.DefineDummy(DummyMap, assetClass);
        ClassPackage = FName.DefineDummy(DummyMap, classPackage);
        TagsAndValues = tagsAndValues is null
            ? new Dictionary<FName, string>()
            : tagsAndValues.ToDictionary(kv => FName.DefineDummy(DummyMap, kv.Key), kv => kv.Value);
        TaggedAssetBundles = null;
        ChunkIDs = chunkIDs ?? Array.Empty<int>();
        PackageFlags = packageFlags;
    }

    public FAssetData(FAssetRegistryArchive Ar)
    {
        if (Ar.Header.Version < FAssetRegistryVersionType.RemoveAssetPathFNames)
        {
            Ar.SkipFName(); // oldObjectPath
        }

        PackagePath = Ar.ReadFName();

        if (Ar.Header.Version >= FAssetRegistryVersionType.ClassPaths)
        {
            // FTopLevelAssetPath 是 (PackageName, AssetName) 两段，上游只留了后一段。
            var classPath = new FTopLevelAssetPath(Ar);
            ClassPackage = classPath.PackageName;
            AssetClass = classPath.AssetName;
        }
        else
        {
            ClassPackage = FName.DefineDummy(DummyMap, "None");
            AssetClass = Ar.ReadFName();
        }

        if (Ar.Header.Version < FAssetRegistryVersionType.RemovedMD5Hash)
        {
            Ar.SkipFName(); // oldGroupNames
        }

        PackageName = Ar.ReadFName();
        AssetName = Ar.ReadFName();

        if (Ar.Header.Version >= FAssetRegistryVersionType.RemoveAssetPathFNames && !Ar.IsFilterEditorOnly)
        {
            Ar.ReadFName(); // OptionalOuterPath, always "None"
        }

        Ar.SerializeTagsAndBundles(this);

        if (Ar.Ver >= ObjectVersion.VER_UE4_CHANGED_CHUNKID_TO_BE_AN_ARRAY_OF_CHUNKIDS)
        {
            ChunkIDs = Ar.ReadArray(Ar.ReadInt32);
        }
        else if (Ar.Ver >= ObjectVersion.VER_UE4_ADDED_CHUNKID_TO_ASSETDATA_AND_UPACKAGE)
        {
            ChunkIDs = new[] { Ar.ReadInt32() };
        }
        else
        {
            ChunkIDs = Array.Empty<int>();
        }

        PackageFlags = Ar.Ver >= ObjectVersion.VER_UE4_COOKED_ASSETS_IN_EDITOR_SUPPORT
            ? (EPackageFlags)Ar.ReadUInt32()
            : EPackageFlags.PKG_None;
    }

    public string ObjectPath => $"{PackageName}.{AssetName}";
}
