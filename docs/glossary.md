# 术语表

本文统一项目内术语的口径。**新增术语必须先查本表，不要另造说法。**

## 资产与包

| 术语 | 含义 |
|---|---|
| **cooked** | 经过 Unreal 烘焙、供运行时加载的资产形态（`.uasset` + `.uexp`），与编辑器里的源资产不同 |
| **UAsset** | 一个 UE 包文件（`.uasset` 头 + `.uexp` 载荷） |
| **Import / Export** | 包内的两张引用表。Export 是本包产出的对象，Import 是本包引用的外部对象 |
| **Outer 链** | 对象的父级关系链（`/Game/A/B.C` 中 `B` 是 `C` 的 Outer）。**Import 的 `OuterIndex` 不一定是 0** |
| **`FPackageIndex`** | 指向 Import/Export 表的索引，**包内局部**。换一个资产就完全失效 |
| **AssetRegistry.bin** | 引擎的资源索引缓存文件，记录每个资产的路径、类名与 Tags，不含资产内容 |

## AssetRegistry 相关

| 术语 | 含义 |
|---|---|
| **`FAssetData`** | 注册表中的一条资产记录：`PackageName` / `PackagePath` / `AssetName` / `AssetClass` / `TagsAndValues` |
| **`FAssetRegistryState`** | 完整解析结果：资产数组 + 依赖节点 + 包数据 |
| **`FStore`** | 注册表内部的字符串存储区（AnsiString / WideString 表），Tags 的值都存在这里 |
| **`FName`** | UE 的名字类型，由字符串 + number 后缀组成（如 `ZenLogo_64`） |
| **语义级回写** | 把解析结果重新序列化成**合法的** bin。输出合法、读回一致，但**字节与原文件不同** |
| **字节级一致** | 输出与输入逐字节相同。**当前不承诺**，要字节一致必须保留原始 `FValueId` 类型 |
| **version 21** | `FAssetRegistryVersionType.ExternalActorToWorldIsEditorOnly`。KARDS 实测版本；也是 UAssetRegistry 建议的写入版本（v24 有 16 字节对齐问题） |

## 映射文件

| 术语 | 含义 |
|---|---|
| **usmap** | UE 属性名映射文件，可据此还原属性名与类型 |
| **JMAP** | 新式映射文件，比 usmap 包含更多信息（KARDS 样本为 66 MB）。格式细节尚未记录 |

## 字节码

| 术语 | 含义 |
|---|---|
| **Kismet** | UE 蓝图的虚拟机字节码 |
| **表达式（Expression）** | 字节码的基本单元，按数组顺序存放，不是字节流 |
| **`EX_Jump` / `EX_JumpIfNot`** | 跳转指令。**其偏移不等于表达式数组索引**，具体规则随版本变化 |
| **UKAPI** | 本项目的**单向**生成器：`FunctionDescription` → Kismet 结构。不是反编译器 |
| **AssetLinker** | 把语义符号解析成当前包内合法引用的模块 |
| **生成路径** | 面向新建函数，走 UKAPI |
| **最小编辑路径** | 面向既有函数，只允许改参数与常量值（见 [ADR 0002](decisions/0002-dual-path-bytecode.md)） |

## 工程

| 术语 | 含义 |
|---|---|
| **WASM** | WebAssembly，浏览器里运行 .NET 的形态 |
| **Blazor WASM** | 用 C# 写前端、编译成 WASM 在浏览器里跑 |
| **submodule** | git 子模块，用来引入 UAssetRegistry 等既有库的源码 |
| **Flare** | 本项目使用的 Blazor 组件库（190+ 组件，零第三方 CSS）。**Flare 本身不带主题**，设计系统由 `Flare.Theme.*` 独立包提供；本项目用 MD3 |
| **MD3** | Material Design 3，Flare 的设计系统之一。主题、调色板、明暗三个轴可在运行时独立切换 |
| **flare-* 令牌** | Flare 的语义化 CSS 变量（`--flare-color-*`、`--flare-shape-*` 等），主题靠换这些变量重绘 |
| **fixture** | 测试用的样本文件。**必须是可合法分发的**，且不进仓库 |
| **golden 对比** | 把移植版输出与桌面版输出逐字段比对，作为回归基线 |

## 状态标签

| 标签 | 含义 |
|---|---|
| `PROPOSED`（提议） | 只有设计，没有代码 |
| `IMPLEMENTED`（已实现） | 代码存在，但正确性未证明 |
| `TESTED`（已测试） | 自动化或手工测试确实跑过并通过 |
| `PARTIALLY SUPPORTED`（部分支持） | 只有声明的子集验证过 |
| `UNVERIFIED`（未验证） | 没有可靠测试证据 |
| `UNSUPPORTED`（不支持） | 有意不支持 |
