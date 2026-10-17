# JMAP Extractor

**状态：`IMPLEMENTED`（已实现）+ `TESTED`（已测试，2026-10-17 对真实 JMAP 跑通）。**

把 `trumank/jmap` 导出的活对象转储（JSON）抽成 UKAPI 需要的两份表，各带一份 Kards-only 缓存版。

## 用法

```bash
dotnet run --project tools/JmapExtractor -c Release -- \
  <jmap.json> <outDir> [AssetRegistry.bin]
```

第三个参数可选：给了就把 `AssetRegistry.bin` 里的 `/Game` 对象并进对象表。

实测：63.2 MB JMAP + 6.2 MB bin → **约 3 秒**，峰值内存约 1.2 GB。

## 产出

| 文件 | 内容 | 实测大小 |
|---|---|---|
| `objects.json` | 全量对象表，**47,311** 条 | 8.94 MB |
| `objects.kards.json` | 只含 4 个 Kards 包，1,870 条 | 0.32 MB |
| `functions.json` | 函数表，1,216 个宿主 / 13,064 个函数 | 4.73 MB |
| `functions.kards.json` | 同上，94 个宿主 / 1,416 个函数 / 2,653 个参数 | 0.45 MB |

对象表构成：**jmap 29,675 + `/Game` 资产 16,250 + 合成包节点 1,386 = 47,311**。

「Kards 模块」= `/Script/kards`、`/Script/KardsCore`、`/Script/KardsSubsystems`、`/Script/KardsWidgets`。

## 1. objects.json —— 用于构建 import 表

```json
{
  "metadata": {
    "extractor": "Solstice.Tools.JmapExtractor",
    "generated_at": "2026-10-17T...",
    "source": "kards-Win64-Shipping-localhost.exe",
    "build": "++UE5+Release-5.6-Fork-CL-0",
    "engine": "5.6",
    "counts": { "objects": 29675, "functionOwners": 1216, "functions": 13064 }
  },
  "objects": {
    "/Script/kards.CardFunctionsStub": {
      "type": "Class",
      "outer": "/Script/kards",
      "class": "/Script/CoreUObject.Class",
      "super": "/Script/Engine.Actor"
    }
  }
}
```

字段刻意只有 4 个，全部服务于 import 条目构建：

| 字段 | 用途 |
|---|---|
| `type` | `Package` / `Class` / `Struct` / `Function` / **`Asset`**（来自注册表） |
| `outer` | 拼 Outer 链；`null` 表示顶层包 |
| `class` | 该对象的类（对应 Import 的 ClassObject），**完整路径**如 `/Script/Engine.Blueprint` |
| `super` | 继承父类（对应 Import 的 SuperStruct）；`/Game` 资产没有此信息 |
| `source` | `jmap` 或 `registry`，标明这条记录来自哪份数据 |

### `/Game` 部分怎么来的

`AssetRegistry.bin` 没有 import 表（`FPackageIndex` 是包内局部的），它能给的是 **`/Game` 侧的全局对象清单**：

- **资产条目**：`type: "Asset"`，`class` 由注册表里的 `FTopLevelAssetPath` 还原（`ClassPackage + "." + AssetClass`）。
- **合成包节点**：为每个 `PackagePath` 及其所有祖先补一条 `type: "Package"`，`class` 固定 `/Script/CoreUObject.Package`，让 `outer` 链能一路走到 `/Game`。

例：

```json
"/Game/Blueprints/Cards/ANZAC/specialCards/card_unit_x.card_unit_x": {
  "type": "Asset",
  "outer": "/Game/Blueprints/Cards/ANZAC/specialCards",
  "class": "/Script/Engine.Blueprint",
  "super": null,
  "source": "registry"
}
```

## 2. functions.json —— 用于节点构建与推荐

```json
{
  "objects": {
    "/Script/kards.CardFunctionsStub": {
      "type": "Class",
      "super": "/Script/Engine.Actor",
      "functions": {
        "AddGameplayRestriction": {
          "flags": "FUNC_Event | FUNC_Public | FUNC_BlueprintCallable | FUNC_BlueprintEvent",
          "params": [
            { "name": "side", "type": "EnumProperty", "dir": "in",
              "class": null, "enum": "/Script/kards.ESideEnum", "offset": 0, "size": 1 },
            { "name": "CardID", "type": "IntProperty", "dir": "in",
              "class": null, "enum": null, "offset": 4, "size": 4 }
          ]
        }
      }
    }
  }
}
```

`dir` 由 JMAP 的 flags 推出：`CPF_ReturnParm` → `return`，`CPF_OutParm` → `out`，其余 → `in`。

## 已知限制

1. **`functions.json` 只含「有函数的对象」**（1,216 个），不是全部 29,675 个对象。要完整对象信息查 `objects.json`。
2. **Kards-only 版不含参数类型的定义**。例如某参数是 `/Script/Engine.Foo`，它在 kards 缓存里查不到，需要回落到全量 `objects.json`。
3. **`/Game` 只有资产级条目**。注册表不描述包内子对象，也不含 import 表；`/Game` 资产没有 `super`（要读 uasset 才知道父类）。
4. **没有函数体**。`CardFunctionsStub` 的函数是 `FUNC_BlueprintEvent`，C++ 只声明、蓝图实现；字节码在 `/Game` 的 uasset 里，JMAP 只给签名。
5. **类布局是分段的**。`Class.properties` 只含本类自有字段，完整布局要沿 `super` 链累加（父类在前）。
