# 项目愿景与范围

**状态：方向已确认（DECIDED）；技术可行性尚未验证（UNVERIFIED）。**
**记录日期：2026-10-17　最近更新：2026-10-17**

本文记录项目的目标、边界与已拍板的决定。凡标为「待确认」的条目，都是需要用户或实验给出结论的开放问题，不得当作已知事实使用。

## 1. 一句话定义

Solstice 是 CCB-TEAM 面向 **KARDS** 的**纯浏览器 UE 工具工作台**：用户在浏览器里打开 cooked 资产，查看包结构、编辑资产注册表、查看 Kismet 字节码，再把结果导出。

解析能力**不重写**，来自团队已有的 C# 库（UAssetRegistry / KismetDecompiler / Prism），Solstice 负责把它们搬进 WASM，并补上浏览器端的交互与工作流。

## 2. 已确认的方向

| 维度 | 决定 | 直接影响 |
|---|---|---|
| 目标游戏 | **KARDS**（与 fyserver / KardsSim 同一个客户端） | 资产路径、包结构、游戏侧怪癖一律以 KARDS 实测为准 |
| 目标引擎 | Unreal Engine **5.6.1**（构建标签 `+++UE5+Release-5.6-Fork-2352d15b`；`Fork` 仅为命名，用户确认序列化格式未改） | 兼容性矩阵只针对 5.6 填写；与 AssetRegistryTool 的 5.6.1 对齐 |
| 用途 | 给 KARDS 做 **Mod** | 需要明确分发边界、游戏侧校验方式、失败可回滚 |
| 交付形态 | **纯浏览器 Blazor WASM** | 无本地进程；受浏览器内存上限与文件 API 限制 |
| 产品定位 | **统一工作台**：资产浏览 + AssetRegistry 编辑 + 字节码查看 | 一个壳、多块面板；但首期只做一块，不并行铺开 |
| 代码复用 | **移植 / 改造**现有 C# 库，不重写解析器 | 必须先评估 WASM 兼容性（反射、AOT、文件 IO） |
| 文件进出 | **拖拽单文件 + 下载导出** | 工具不做目录遍历 |
| pak 处理 | **浏览器内不解包**，先用 Prism 解出散文件 | Solstice 只处理单个 `.uasset` / `.bin` / `.locres` |
| 字节码能力 | **双路径（方案 C）**：新建函数走 UKAPI 单向生成；既有函数只允许改参数与常量值 | 两套代码：生成器 + 最小编辑器；后者仍需处理跳转偏移 |
| 使用者 | 团队内部数人 | UI 以「可诊断、可回滚」为主，不需要面向玩家的引导与防错 |
| 首个垂直切片 | **AssetRegistry.bin 读取与编辑** | 先证明「能正确解析」，再谈「能安全写回」 |
| AssetRegistry 编辑目的 | 把**新增 / 替换的资产登记进原版注册表** | 等价于把 AssetRegistryTool 的 Generate / Merge 搬到浏览器 |
| 资产加密 | pak 使用 AES，**包内资产未加密**；无签名校验 | 单个 `.uasset` / `.bin` 可直接解析，不需要解密逻辑 |
| 文档语言 | **中文** | 仓库文档全部改为中文，技术标签保留英文原文 |

## 3. 与 CCB-TEAM 现有工具链的关系

这是本项目最重要的上下文：**Solstice 不是从零开始，它是既有工具链的浏览器前端。**

| 现有项目 | 语言 / 运行时 | 对 Solstice 的价值 |
|---|---|---|
| [UAssetRegistry](https://ccb-team.github.io/projects/uassetregistry.html) | C# / .NET 10 + UAssetAPI 1.1.0 | `AssetRegistry.bin` 解析、快速枚举、语义级回写——首期切片的全部核心逻辑 |
| [AssetRegistryTool](https://ccb-team.github.io/projects/assetregistrytool.html) | C++ UE 5.6.1 插件 | 定义了「Generate / Merge / 覆盖开关」这套语义，浏览器端要复刻的是**它的行为**，不是它的实现 |
| [KismetDecompiler](https://ccb-team.github.io/projects/kismetdecompiler.html) | C# | 字节码 AST 与 CFG 结构化，字节码面板的基础 |
| [KismetReactor](https://ccb-team.github.io/projects/kismetreactor.html) | C# WPF | 交互参照物：跳转可视化、就地修改。Solstice 是它的 Web 版 |
| [Prism](https://ccb-team.github.io/projects/prism.html) | C# | `.pak` 浏览与资源提取；决定「要不要直接打开 pak」 |
| [ULocres](https://ccb-team.github.io/projects/ulocres.html) | C# | `.locres` 读写，未来本地化面板可复用 |

**由此推出的关键判断：**

1. **纯浏览器方案是可行的**——上述库都是纯托管 C#，理论上可被 Blazor WASM 引用。
2. **真正的工作量在「移植」而不是「实现」**——难点是 WASM 下的内存、AOT/反射裁剪、以及把「读文件路径」改成「读 `Stream` / `byte[]`」。
3. **UI 是新的，解析不是**——文档里不应再出现「验证 UAssetAPI 是否支持 XX」这种前提未定的表述，而应改为「验证 UAssetRegistry 在 WASM 下是否可运行」。

## 4. 由上述决定推出的硬约束

1. **浏览器内存**：WASM 内存有实际上限（通常约 2 GB）。已测样本中 `AssetRegistry.bin` 为 6.2 MB、最大的 `.uasset` 仅 23 KB，**内存不是当前瓶颈**；但仍必须设大小上限与失败提示，避免将来遇到大包时静默崩溃。
2. **无文件系统访问**：纯前端拿不到路径，只能通过拖拽获得 `File` 对象，通过下载或 File System Access API 写回。任何「批量处理整个目录」的设计在当前形态下都不成立。
3. **不做 pak 解包**（已确认）：用户先用 Prism 等外部工具把文件取出，Solstice 只处理散文件。
4. **无签名校验**：修改后的文件在签名层面不会被拒绝，但这不等于游戏会接受——仍可能存在 CRC、版本号、内容哈希等其它校验。
5. **单向生成**：UKAPI 只做「高层描述 → 字节码」，不承诺把既有字节码还原成同一套高层模型（见 `docs/decisions/0001-one-way-ukapi.md`）。
6. **语义级回写 ≠ 字节级一致**：UAssetRegistry 的回写会重建 name 表与 FStore，输出合法但字节不同。任何「保留原文件未改动部分」的承诺都必须先证明。

## 5. 明确不做的事（Non-goals）

- 不做完整的 Blueprint 反编译器，不做双向语义往返编辑。
- 不在首期实现 pak 打包 / 解包（除非第 6 节确认纳入）。
- 不做 C++ 代码生成，不做引擎侧插件（那是 AssetRegistryTool 的职责）。
- 不在仓库中提交任何 KARDS 资产、密钥、解密后的原始资源或其它受版权保护的内容。
- 不把「序列化成功」当作「运行时正确」。

## 6. 待确认问题（阻塞级）

| # | 问题 | 为什么阻塞 | 如何解决 |
|---|---|---|---|
| 1 | 移植后的库如何验证「与桌面版行为一致」？ | 没有回归基线就会悄悄改变输出 | 建立 golden fixture 对比测试 |
| 2 | 改完的 `AssetRegistry.bin` 如何回到游戏？ | pak 内文件无法直接替换 | 用户说明工作流 + 实测 |
| 3 | 游戏是否存在 pak CRC / 完整性校验？ | 决定「改完能不能直接跑」 | 实测 |
| 4 | JMAP（66 MB）的具体格式？ | 用户已说明是「新式映射文件，含更多信息」，但格式细节未记录 | 解析样本后补文档 |

> 已解决：KARDS 客户端确认为 **UE 5.6.1**（构建标签 `+++UE5+Release-5.6-Fork-2352d15b`）；`AssetRegistry.bin` 实测为 **v21** 格式。
> 已解决：字节码能力采用**方案 C（双路径）**——新建函数走 UKAPI 单向生成，既有函数只允许改参数与常量值。
> 已解决：pak 不在浏览器内解包，先用 Prism 取出散文件。
> 已解决：真实样本已就位，清单见 [`docs/samples.md`](samples.md)。

## 7. 已知风险

- **AssetRegistry 只是缓存**：只改 registry 而不改资产本体，运行时很可能不生效。KARDS 若在运行时依赖 registry 做资源查找，改它才有意义；否则真正的落点仍是 pak 与包体。这一条必须实测确认。
- **WASM 移植成本被低估**：.NET 10 + UAssetAPI 在浏览器里跑，可能撞上 AOT 裁剪、反射、`File`/`Path` API 缺失、大数组 GC 压力等一串问题。
- **与桌面工具漂移**：两套实现（桌面版库 + 浏览器移植版）长期维护，容易行为分叉。
- ~~**样本缺失**~~：已解决。真实样本已就位，清单见 [`docs/samples.md`](samples.md)。
