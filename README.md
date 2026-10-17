# Solstice

**Solstice** 是一个技术概念仓库，围绕 Unreal Engine cooked 资产与单向 Kismet 字节码生成，收录研究、架构说明、实现计划、Harness 提示词以及相互隔离的代码实验。

> **状态：概念 / 早期工程计划。** 本仓库不声称每一项计划中的能力都已实现或已验证。
>
> 目标游戏为 **KARDS**（UE 5.6.1），解析能力计划通过**移植 CCB-TEAM 既有 C# 库**获得，而不是从零实现。完整定位见 [docs/vision.md](docs/vision.md)。

## 项目方向

Solstice 探索一套浏览器优先的工作流，用于检查 Unreal Engine cooked 资产，并通过单向 API 生成/替换选定函数的字节码。

核心原则：

- **单向生成：** 高层函数描述编译为受支持的 Kismet 结构。不承诺既有字节码能经由反编译器完成往返。
- **资产本地引用：** `FPackageIndex` 值属于特定的包。全局元数据绝不能被视为当前资产 Import/Export 表的替代品。
- **实现之前先取证：** 对照真实样本验证 UAssetAPI 的实际 API、目标 UE 版本以及序列化行为。
- **安全写回：** 生成到独立的输出，验证它，然后重新读取它，之后才宣布成功。
- **如实报告能力：** 不受支持的表达式与未验证的行为必须明确标注。

## 仓库地图

- [`docs/vision.md`](docs/vision.md) — **先读这个**：项目定位、目标游戏、已确认的方向与开放问题。
- [`docs/samples.md`](docs/samples.md) — 本机真实样本清单（路径、大小、版本），以及它们能证明什么。
- [`docs/glossary.md`](docs/glossary.md) — 术语表，统一口径。
- [`docs/project-structure.md`](docs/project-structure.md) — 解决方案结构、submodule、构建与部署。
- [`docs/architecture.md`](docs/architecture.md) — 系统边界与核心设计决策。
- [`docs/implementation-roadmap.md`](docs/implementation-roadmap.md) — 分阶段交付计划与验收标准。
- [`docs/compatibility.md`](docs/compatibility.md) — 基于证据的兼容性矩阵。
- [`docs/bytecode-generation.md`](docs/bytecode-generation.md) — 生成与验证规则。
- [`prompts/harness-master-prompt.md`](prompts/harness-master-prompt.md) — 面向编码代理的主提示词。
- [`prompts/phase-task-template.md`](prompts/phase-task-template.md) — 可复用的阶段任务提示词。
- [`snippets/`](snippets/) — 规模小、各自独立成文的代码概念。
- [`experiments/`](experiments/) — 实验、观察记录与未解决的问题。
- [`docs/decisions/`](docs/decisions/) — 架构决策记录。

## 初始状态

| 领域 | 状态 |
|---|---|
| 产品与架构概念 | 已起草 |
| Harness 提示词 | 初稿 |
| UAssetAPI API 验证 | 尚未记录（上游库版本 1.1.0，见 UAssetRegistry） |
| AssetRegistry 读写验证 | **样本已就位**：KARDS v21 / 6.2 MB，解析尚未运行 |
| Kismet 生成验证 | 尚未记录 |
| 真实资产集成测试 | **样本已就位**：2137 个 `.uasset` / 24.5 MB，尚未运行 |
| 浏览器 UI | 本概念仓库中未实现 |

## 工作规则

1. 不要提交游戏资产或其它没有明确再分发权利的文件。
2. 除非测试确实运行过且其结果已记录，否则不要声称测试通过。
3. 不要臆造 API、表达式布局、字节偏移或兼容性结论。
4. 把概念性提议与已验证的实验分开。
5. 宁要小而完整的端到端垂直切片，不要大量未测试的代码倾倒。

## 建议的首个里程碑

在把完整 UI 铺开之前，先验证实际工具链与一个最小的资产工作流：

1. 锁定 .NET SDK 与依赖版本。
2. 验证 UAssetAPI 对计划中的 AssetRegistry 与 UAsset 操作的实际支持情况。
3. 加载一个可合法分发的样本文件。
4. 定位目标函数的导出，并检查可用的字节码表示。
5. 为生成一个简单且受支持的函数做原型。
6. 写入新文件，重新读取，并记录结果。

参见[实现路线图](docs/implementation-roadmap.md)。
