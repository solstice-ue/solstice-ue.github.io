# 本机真实样本清单

**状态：`OBSERVED`（已观察）**——以下全部是 2026-10-17 在本机实际测量到的文件事实，不是二手推断。

样本**不进仓库**（版权与体积原因），只在本地用于验证。本文只记录「有什么、多大、什么版本」，不记录文件内容。

## 1. AssetRegistry.bin

| 项 | 值 |
|---|---|
| 路径 | `D:\devenv\fmodel\Output\Exports\kards\AssetRegistry.bin` |
| 大小 | 6.2 MB |
| 头部 GUID | `E79E7F71-3A49-B0E9-3291-B3880781381B`（UE 固定标识） |
| version | **21**（`ExternalActorToWorldIsEditorOnly`，FixedTags 之后的格式） |
| bFilterEditorOnly | 1 |

结论：这份文件落在 UAssetRegistry 已验证的格式区间内（该库明确建议写 v21、不要用 v24 的 16 字节对齐）。**首期切片有真实样本可用。**

## 2. Blueprints 导出集

| 项 | 值 |
|---|---|
| 路径 | `D:\devenv\fmodel\Output\Exports\kards\Content\Blueprints` |
| 文件数 | 4274（**2137** 个 `.uasset` + **2137** 个 `.uexp`） |
| 总大小 | 24.5 MB |
| 单个最大 | `BP_3DRewards.uexp` ≈ 23 KB |

结论：资产浏览与字节码面板都有样本可用，且体积很小（最大 23 KB），**浏览器内存完全不是问题**——之前担心的「大 uasset 压垮 WASM」在这批样本上不成立。

### 2.1 卡牌资产（`Content/Blueprints/Cards`）

| 项 | 值 |
|---|---|
| `card_*` 数量 | **2019** |
| 类型前缀分布 | `card_unit` 1023、`card_event` 785、`card_location` 179、`card_wildcard` 24、`card_display` 4、`card_brawl` 2、`card_sideeffect` 2 |
| 目录结构 | `Cards/<国家>/<系列>/<类型>/card_*.uasset` |
| 国家目录 | ANZAC、Britain、Finland、France、Germany、Italy、Japan、Neutral、Poland、Soviet、USA，另有 BrawlCards、PromoCards、Render、Tutorials、Visuals、Wildcards、ZSideEffectHolders |
| 系列目录（USA 下抽样） | Allegiance、Base、BloodIron、Breakthrough、BrothersInArms、CovertOp、Homefront、Legions、locations、NavalWarfare、OceaniaStorm、specialCards、ToW、WinterWar、WorldAtWar |

命名示例：`card_unit_16th_infantry_brigade_vet`、`card_event_aans`、`card_location_alam_el_halfa`、`card_wildcard_elite_britain`。

**待确认**：导出目录 `Content/Blueprints/…` 到游戏内 `PackagePath` 的映射（当前推断为 `/Game/Blueprints/…`），以及卡牌的 `AssetClass`。两者都必须用真实样本核对后才能写进写回逻辑。

## 3. Paks 目录

| 文件 | 大小 | 说明 |
|---|---|---|
| `kards-5.6.1-0+++UE5+Release-5.6-Fork-2352d15b.usmap` | 1.9 MB | UE 版本映射，文件名暴露引擎构建标签 |
| `kards-Android_ASTC.pak` | 1.17 GB | Android ASTC 纹理包 |
| `kards1.60_No_UE4SS.jmap` | 66 MB | **新式映射文件，比 usmap 包含更多信息**（用户口述，格式细节待记录） |

**从文件名读出的两个事实：**

1. 引擎构建标签是 `+++UE5+Release-5.6-Fork-2352d15b`——**带 `Fork` 字样**，说明 KARDS 用的不是纯官方 5.6 构建。是否改动过序列化格式尚未验证。
2. 已经有了 `.usmap`，意味着属性名映射可以直接获得，不需要靠猜。

## 4. 这些样本能证明什么 / 不能证明什么

**能证明：**

- `AssetRegistry.bin` 确实是 v21 格式，头部结构符合预期。
- 有 2137 个可解析的蓝图资产，规模足够做真实回归测试。
- 引擎版本是 5.6.1 系（带 Fork 标签）。

**不能证明：**

- 解析**正确性**——头部读对不代表资产数组、FStore、依赖节点都读对。
- 回写**正确性**——语义级回写会产生与原文件不同的字节。
- **运行时**是否生效——游戏是否读这份文件、是否有 CRC 校验，都还没测。

## 5. 使用约束

- 样本不得提交进仓库，不得随发行版分发。
- 任何写入实验都必须输出到新文件，不覆盖样本原件。
- 引用样本数据进文档时，只写统计量（数量、大小、版本号），不写资产内容。
