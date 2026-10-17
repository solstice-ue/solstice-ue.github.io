# JMAP 分析：CDO / 函数签名 / import 表

**状态：`TESTED`（已测试）—— 2026-10-17 对 `kards1.60_No_UE4SS.jmap`（63.2 MB）全量解析。**

样本：`D:\files\projects\kards\Content\Paks\kards1.60_No_UE4SS.jmap`
来源：`trumank/jmap` 对运行中进程 `kards-Win64-Shipping-localhost.exe` 的**活对象转储**（JSON）。
引擎：UE 5.6，构建标签 `++UE5+Release-5.6-Fork-CL-0`。

## 总览

| 项 | 值 |
|---|---|
| 对象总数 | **29,675** |
| 包数 | **211**，**全部在 `/Script` 下** |
| `/Game` 对象 | **0** |
| 对象类型 | Function 13,064 / ScriptStruct 5,349 / Object 4,819 / Class 4,356 / Enum 1,876 / Package 211 |

## 1. CDO —— 有，4,818 个

- 命名规律：`Default__<ClassName>`，例如 `/Script/ACLPlugin.Default__AnimBoneCompressionCodec_ACL`。
- 每条 CDO 都带 `class` 字段指向其 Class 对象，**4,818 条全部可解析**（0 悬空）。
- `outer` 指向所属包，因此可以建立 `Class → CDO` 的双向映射。

**结论**：CDO 可直接用于「按类找默认对象」，这是 UKAPI 解析默认属性值的入口。

## 2. 完整函数签名 —— 有

每个 `Function` 对象都包含：

| 字段 | 含义 |
|---|---|
| `properties[]` | 逐个参数的 `name` / `type` / `size` / `offset` / `array_dim` / `flags` |
| `flags` | `CPF_Parm`（入参）/ `CPF_ReturnParm`（返回值）/ `CPF_OutParm`（出参） |
| `property_class` | 对象类参数的类路径（3,986 处） |
| `enum` | 枚举参数的枚举路径（1,439 处） |
| `function_flags` | `FUNC_Static` / `FUNC_BlueprintCallable` / `FUNC_Native` 等 |
| `properties_size` / `min_alignment` | 参数区大小与对齐 |

参数统计：`CPF_Parm` 26,811、`CPF_ReturnParm` 6,674、`CPF_OutParm` 11,917。

参数类型分布（前 8）：StructProperty 5,759、BoolProperty 5,397、ObjectProperty 3,662、FloatProperty 2,524、IntProperty 2,303、StrProperty 1,696、NameProperty 1,276、ArrayProperty 1,261。

**结论**：足够重建 UKAPI 需要的 `FunctionSignature`（名称、参数序列、类型、返回值、调用约定标志）。

## 3. 可转 import 表 —— `/Script` 侧完整，`/Game` 侧缺失

**闭合性检查（全表 29,675 条）**：

| 检查 | 结果 |
|---|---|
| `outer` 指向不存在的对象 | **0** |
| `class` 指向不存在的对象 | **0** |
| 路径含非 ASCII（FName 非法） | **0** |

也就是说：只要对象在表里，它的完整路径、outer 链、class 链都是自洽的，可以直接拼出 import 条目需要的 `(ClassPackage, ClassObject, Object, Package)` 四元组。

**但是**：211 个包全部是 `/Script/*`，**没有一个 `/Game` 对象**。

原因：JMAP 是**运行时活对象**转储，只包含当时已加载的对象。游戏启动只加载脚本包，不会加载全部 `/Game` 资产。

**结论**：

- `/Script` 侧的 import 条目可以从 JMAP 完整生成。
- `/Game` 侧（卡牌等资产）**必须**来自 AssetRegistry.bin 或目标 uasset 自身的 Import/Export 表。
- 这正好印证 `docs/architecture.md` 的规则：**JMAP 提供全局符号身份，绝不替代包内 Import/Export 表**。

## 对 UKAPI 的直接影响

| 需求 | JMAP 能否满足 |
|---|---|
| 函数签名（参数、返回值、类型） | ✅ 完整 |
| 类继承关系 / CDO 默认值 | ✅ 完整 |
| 枚举值表 | ✅ 完整（1,876 个 Enum，含 `names[]`） |
| 结构体字段布局 | ✅ 完整（5,349 个 ScriptStruct） |
| `/Game` 资产的包路径 | ❌ 需 AssetRegistry.bin |
| 目标资产内的 `FPackageIndex` | ❌ 必须由该资产自己的 Import 表决定 |
