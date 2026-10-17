# UE 源码对照：AssetRegistry 序列化

**来源：`CCB-TEAM/UnrealEngine`（私有 fork，分支 `release`，UE 5.6）。**
**记录日期：2026-10-17。**

本文只记录**从引擎源码直接读到**的事实，用于重写 `UAssetRegistry`。凡未读到源码的部分，明确标注。

## 1. 保存入口

`Engine/Source/Runtime/AssetRegistry/Private/AssetRegistryState.cpp:2512`

```cpp
bool FAssetRegistryState::Save(FArchive& OriginalAr, const FAssetRegistrySerializationOptions& Options)
{
    FAssetRegistryHeader Header;
    Header.Version = FAssetRegistryVersion::LatestVersion;
    Header.bFilterEditorOnlyData = OriginalAr.IsFilterEditorOnly();
    Header.SerializeHeader(OriginalAr);

    FAssetRegistryWriter Ar(FAssetRegistryWriterOptions(Options), OriginalAr);

    int32 AssetCount = CachedAssets.Num();
    Ar << AssetCount;                       // ← 写进 MemWriter（body）

    // 资产按 ObjectPath 排序后逐个写
    for (FAssetData* Asset : GetMutableAssetsSortedByObjectPath())
        Asset->SerializeForCache(Ar);

    // 依赖段：先占位 int64，写完回填
    int64 OffsetToDependencySectionSize = Ar.Tell();
    int64 DependencySectionSize = 0;
    Ar << DependencySectionSize;
    int64 DependencySectionStart = Ar.Tell();
    ...  // 写 NumDependencies + 各节点
    int64 DependencySectionEnd = Ar.Tell();
    DependencySectionSize = DependencySectionEnd - DependencySectionStart;
    Ar.Seek(OffsetToDependencySectionSize);
    Ar << DependencySectionSize;
    Ar.Seek(DependencySectionEnd);

    // 包数据段
    int32 PackageDataCount = 0;
    if (Options.bSerializePackageData) { ... Ar << PackageDataCount; ... }
    else                               { Ar << PackageDataCount; }
}
```

**要点**：资产必须按 `ObjectPath` 排序写出；依赖段长度是「回填」的 int64。

## 2. 最终布局（关键）

`Engine/Source/Runtime/AssetRegistry/Private/AssetRegistryArchive.cpp:220`

```cpp
FAssetRegistryWriter::~FAssetRegistryWriter()
{
    int64 BodySize = MemWriter.TotalSize();
    UE::AssetRegistry::AlignPosInArchive(MemWriter);   // 对齐
    int64 StoreStart = MemWriter.TotalSize();
    SaveStore(Tags.Finalize(), *this);                 // store 追加在 body 之后

    // 真正的写出顺序：
    SaveNameBatch(FlattenIndex(Names), TargetAr);      // 1) name batch
    UE::AssetRegistry::AlignPosInArchive(TargetAr);    // 2) 对齐
    TargetAr.Serialize(Store 字节);                     // 3) store
    TargetAr.Serialize(Body 字节);                      // 4) body
}
```

所以文件是：

```
Header → NameBatch → (对齐填充) → Store → Body[AssetCount + Assets + 依赖段 + 包数据段]
```

**注意**：这与我原先以为的「header → nameBatch → store → body」一致，但确认了**对齐填充**的存在，且 store 与 body 在内存里是分开构建后拼接的。

## 3. FName 的序列化

`AssetRegistryArchive.cpp:238`（写）与 `:338`（读）

```cpp
// 写
uint32 Index = Names.FindOrAdd(EntryId, Names.Num());
if (Value.GetNumber() != NAME_NO_NUMBER_INTERNAL) {
    Index |= AssetRegistryNumberedNameBit;   // 0x80000000
    return *this << Index << Number;         // 8 字节
}
return *this << Index;                       // 4 字节

// 读
*this << Index;
if (Index & AssetRegistryNumberedNameBit) { Index -= Bit; *this << Number; }
Out = Names[Index].ToName(Number);
```

**要点**：带数字后缀的 FName 占 8 字节，否则 4 字节。上游 C# 实现写的是 `index | 0x80000000` 再跟 number —— 与源码一致。

## 4. Tag map 的句柄

`AssetRegistryArchive.cpp:255` / `:365`

```cpp
void SaveTags(FAssetRegistryWriter& Writer, const FAssetDataTagMapSharedView& Map) {
    uint64 MapHandle = Writer.Tags.AddTagMap(Map).ToInt();
    Writer << MapHandle;      // 只写 8 字节句柄，不写内容
}

FAssetDataTagMapSharedView LoadTags(FAssetRegistryReader& Reader) {
    uint64 MapHandle;
    Reader << MapHandle;
    return FAssetDataTagMapSharedView(FixedTagPrivate::FPartialMapHandle::FromInt(MapHandle).MakeFullHandle(Reader.Tags->Index));
}
```

**要点**：资产记录里只有 8 字节句柄，真正的键值存在全局 store 里。句柄的位布局在 `FPartialMapHandle`（未读到源码），但上游 C# 与参考实现都用 `(bit63, count<<32, startIndex)`。

## 5. Bundles 的序列化

`AssetRegistryArchive.cpp:119`（写） / `:139`（读）

```
int32 Num
for each bundle (按 BundleName 排序):
    FName BundleName
    int32 NumAssets
    for each path (排序后):
        FSoftObjectPath::SerializePath(Ar)   // 版本 >= RemoveAssetPathFNames 时是 2 个 FName
    FString SubPathString
```

## 6. 尚未读到的部分

| 内容 | 状态 |
|---|---|
| `SaveNameBatch` / `LoadNameBatch` 的确切字节布局 | 只读到头文件声明；`NameBatchSerialization.cpp` 未在该 fork 的预期路径找到 |
| `FixedTagPrivate::SaveStore` / `LoadStore` 的字段顺序 | `AssetDataTagMapSerializationDetails.h` 未找到 |
| `AlignPosInArchive` 的对齐单位 | 未读到；实测样本 name batch 结束后**没有填充**（store magic 紧接其后） |
| 版本 21 的格式差异 | 参考实现（CUE4Parse 移植）只覆盖到 18；样本是 21，store 段按 18 的布局解析会在结尾处失配 |

## 7. 对重写的结论

1. **写出的顺序与排序规则已有权威依据**（本文第 1–5 节）。
2. **name batch 与 store 的字节布局，应以我们自己的读取器为准** —— 它已能正确解析版本 21 的样本（16,250 个资产、tags 完整），说明其反序列化逻辑与 v21 一致；序列化器镜像它即可，而不是照抄只支持到 18 的第三方实现。
3. 对齐填充需要按版本判定：实测 v21 样本在 name batch 之后**无填充**。
