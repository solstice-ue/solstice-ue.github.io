# Solstice 架构

**状态：PROPOSED（提议）——尚未对照完整实现验证。**

## 0. 现实基线

本节记录在原有设计之上新增的现实约束，**优先级高于下文各节的抽象描述**。定位与决策背景见 [`vision.md`](vision.md)。

| 项 | 现实 |
|---|---|
| 目标游戏 / 引擎 | KARDS / UE 5.6.1（构建标签带 `Fork`，格式未改） |
| 解析能力来源 | **移植** CCB-TEAM 既有 C# 库，不重写（UAssetRegistry / KismetDecompiler / Prism / ULocres） |
| 依赖引入方式 | **git submodule** 引入源码，允许边移植边改 |
| 运行形态 | 纯浏览器 Blazor WASM |
| 部署 | **GitHub Pages 静态托管**（见 §10） |
| pak | 不在浏览器内解包，由 Prism 预先取出散文件 |
| 字节码 | **双路径**：新建函数走 UKAPI；既有函数只允许改参数与常量值（见 [ADR 0002](decisions/0002-dual-path-bytecode.md)） |

因此下文 §3 的 *UAssetAPI infrastructure* 与 *Metadata* 两项，应理解为**对既有库的适配层**，而不是从零实现的解析器。

## 1. 产品范围

Solstice 是一个面向浏览器优先的 Unreal Engine cooked 资产工具包的概念与工程工作区。计划中的工具把资产检查与选定函数字节码的单向生成和替换结合起来。

它不打算成为完整的 Blueprint 反编译器，也不打算成为双向语义往返编辑器。

## 2. 主要工作流

### 只读检查

`Local file → asset adapter → asset model → inspector → read-only expression tree`

检查器可以定位目标对象、显示 Import/Export 数据，并暴露受支持的字节码结构。未知表达式应显示为不受支持或仅部分理解，而不是赋予编造出来的语义。

### 生成与替换

`FunctionDescription → semantic validation → UKAPI → AssetLinker → OutputValidator → replacement preview → serialize to new file → re-read and validate`

每个阶段都必须产出可据以行动的诊断信息。某个阶段失败时，必须阻止把写回工作流报告为成功。

## 3. 拟议模块

- **Client（客户端）：** Blazor UI、文件选择、工作区状态、检查器、生成器表单、进度与诊断。
- **Domain（领域）：** 资产/函数标识、描述、语句、参数、诊断与验证结果类型。
- **Application（应用）：** 工作流、编排、保存策略与应用服务。
- **UAssetAPI infrastructure（基础设施）：** 版本相关的解析、表达式构造、序列化与兼容性适配器。
- **UKAPI：** 从高层函数描述单向编译为受支持的 Kismet 结构。
- **AssetLinker：** 针对当前资产解析语义符号标识，并构建合法的包内局部引用。
- **Metadata（元数据）：** JMAP 加载/索引、可选的 UHT 签名提取，以及元数据预处理。
- **CLI：** 离线索引构建与验证命令，如果确实有用的话。
- **Tests（测试）：** 单元、集成与基于样本的回归测试。

## 4. 元数据与包内局部引用的对比

JMAP 与可选的 UHT 解析可以提供全局符号标识、函数签名、类型、继承关系与包路径。

它们不能替代当前 UAsset 的 Import/Export 表。`FPackageIndex` 是包内局部的。先解析符号标识，再为目标资产查找或构造正确的本地 import/export 关系。

Import 标识必须考虑对象标识与 Outer 链，而不能只看叶子函数名。

## 5. UKAPI 是单向的

UKAPI 消费高层描述并生成受支持的字节码结构。它不承诺把既有函数重建为同一套高层模型。

只读字节码解析对目标发现、检查以及写入后验证仍然有用。

## 6. 控制流与偏移

不要假定 `EX_Jump` 或 `EX_JumpIfNot` 的偏移等于表达式列表索引。要从实际依赖实现与已验证的样本中确定目标版本的序列化规则。如果正确的偏移处理尚未实现并测试，就禁用该生成能力。

## 7. 验证层次

1. 输入模型：必填字段、变量声明、参数数量、受支持的语句形态。
2. 生成的结构：表达式类型、必填字段、嵌套与控制流目标。
3. 引用：索引是否合法、对象类型是否符合预期、函数标识与 Outer 关系。
4. 序列化输出：重新读取输出，确认目标函数与预期结构，并在可行时检测意外的连带改动。

除非确实实现了完整的 VM 栈验证，否则不要声称有。

## 8. 安全写回

- 保留原始输入。
- 在隔离的资产实例或安全副本上操作。
- 在变更被提交到输出之前完成验证。
- 显示替换摘要。
- 默认写入新的输出路径。
- 重新读取输出，并运行序列化后检查。
- 区分「可解析」与「运行时正确」。

## 9. 拟议的依赖方向

`Client → Application → Domain`

基础设施适配器实现应用层所使用的接口。UKAPI 应依赖领域模型与显式的链接/序列化抽象，而不是依赖 UI 组件。

确切的项目引用必须跟随实际实现，并保持不出现不必要的循环依赖。

## 10. 部署与构建（GitHub Pages）

GitHub Pages 是纯静态托管，Blazor WASM 在这里有几个必须提前处理的坑：

| 问题 | 处理方式 |
|---|---|
| `.wasm` / `.dat` / `.blat` 的 MIME 类型 | Pages 不保证正确返回，需要在构建时重命名为 `.bin` 或使用 `blazor.boot.json` 里的自定义后缀 |
| Brotli / Gzip 预压缩 | 启用 `BlazorWebAssemblyLoadAllGlobalizationData` 之外的压缩选项，并确保文件名后缀与 `blazor.boot.json` 一致 |
| 子路径部署 | 若部署在 `/<repo>/` 下，必须设置 `<base href="/<repo>/">` |
| Jekyll 干扰 | 添加 `.nojekyll` 文件，避免下划线开头的目录被吞 |
| 多线程（COOP/COEP） | Pages **无法**设置 `Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy`，因此**不能启用 WASM 多线程**；如需多线程必须换托管方式 |
| 单文件体积 | Pages 单文件上限 100 MB，仓库建议上限 1 GB——WASM 运行时约几 MB，通常不是问题 |

**结论**：可以部署，但「不能用多线程」是硬限制。解析 6.2 MB 的 `AssetRegistry.bin` 单线程足够；将来若遇到大文件，应改为流式处理而不是加线程。
