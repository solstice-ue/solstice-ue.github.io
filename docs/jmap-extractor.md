# JMAP Extractor

**状态：`IMPLEMENTED`（已实现）+ `TESTED`（已测试，2026-10-17 对真实 JMAP 跑通）。**

把 `trumank/jmap` 导出的活对象转储（JSON）抽成 UKAPI 需要的两份表，各带一份 Kards-only 缓存版。

## 用法

```bash
dotnet run --project tools/JmapExtractor -c Release -- \
  <jmap.json> <outDir>
```

实测：63.2 MB 输入 → **2 秒**，峰值内存约 1 GB（`JsonDocument` 全量载入）。

## 产出

| 文件 | 内容 | 实测大小 |
|---|---|---|
| `objects.json` | 全量对象表，29,675 条 | 4.98 MB |
| `objects.kards.json` | 只含 4 个 Kards 包，1,870 条 | 0.29 MB |
| `functions.json` | 函数表，1,216 个宿主 / 13,064 个函数 | 4.74 MB |
| `functions.kards.json` | 同上，94 个宿主 / 1,416 个函数 / 2,653 个参数 | 0.45 MB |

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
| `type` | 判断是 Package / Class / Struct / Function… |
| `outer` | 拼 Outer 链；`null` 表示顶层包 |
| `class` | 该对象的类（对应 Import 的 ClassObject） |
| `super` | 继承父类（对应 Import 的 SuperStruct） |

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
3. **没有 `/Game` 对象**。JMAP 是运行时活对象转储，只含已加载的脚本包。`/Game` 资产路径必须来自 `AssetRegistry.bin`。
4. **没有函数体**。`CardFunctionsStub` 的函数是 `FUNC_BlueprintEvent`，C++ 只声明、蓝图实现；字节码在 `/Game` 的 uasset 里，JMAP 只给签名。
5. **类布局是分段的**。`Class.properties` 只含本类自有字段，完整布局要沿 `super` 链累加（父类在前）。
