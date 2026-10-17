# 贡献指南

面向所有在本仓库工作的人（包括编码代理）。**开始前先读** [`docs/vision.md`](docs/vision.md) 与 [`AGENTS.md`](AGENTS.md)。

## 1. 这个仓库是什么

Solstice 是面向 **KARDS（UE 5.6.1）** 的纯浏览器资产工作台。解析能力来自 CCB-TEAM 既有 C# 库的**移植**，不从零重写。

动手前请先确认你要改的东西属于哪条路径：

| 路径 | 入口文档 |
|---|---|
| 资产注册表（首期） | [`docs/architecture.md`](docs/architecture.md)、[`docs/samples.md`](docs/samples.md) |
| 字节码生成 | [`docs/bytecode-generation.md`](docs/bytecode-generation.md)、[`docs/decisions/0001-one-way-ukapi.md`](docs/decisions/0001-one-way-ukapi.md) |
| 字节码最小编辑 | [`docs/decisions/0002-dual-path-bytecode.md`](docs/decisions/0002-dual-path-bytecode.md) |
| 阶段计划 | [`docs/implementation-roadmap.md`](docs/implementation-roadmap.md) |

## 2. 三条硬规则

1. **不许猜。** 不猜 API、不猜字节偏移、不猜版本行为。查源码、查文档，或写最小程序证明。
2. **不许假装通过。** 测试没跑就是没跑；环境跑不起来要单独说明，不能说成代码失败或代码通过。
3. **不许升级状态标签。** `PROPOSED` 不会因为写了代码就变成 `TESTED`，必须真的跑过并记录结果。

## 3. 样本文件

- **绝不提交**游戏资产、密钥、解密后的资源。
- 本地样本路径见 [`docs/samples.md`](docs/samples.md)，只在本机使用。
- 写回实验一律输出新文件，**不覆盖样本原件**。
- 引用样本数据进文档时只写统计量（数量、大小、版本），不写资产内容。

## 4. 提交规范

- 一个 commit 只做一件事，消息用中文，格式 `类型: 摘要`。
- 类型：`docs` / `feat` / `fix` / `test` / `chore` / `refactor`。
- 文档与代码分开提交。
- 提交前跑 `dotnet build` 与 `dotnet test`，把真实结果写进 PR 描述。

## 5. 文档规范

- 全部中文，技术标签保留英文原文（如 `PROPOSED`（提议））。
- 术语以 [`docs/glossary.md`](docs/glossary.md) 为准。
- 新增架构级决定时，写一份 ADR 放进 `docs/decisions/`，编号递增，不要修改已接受的 ADR 的结论（要推翻就新写一份并标注取代关系）。
- 文档里不要出现没有证据的兼容性结论；`docs/compatibility.md` 只填真正跑过的行。

## 6. 什么时候算做完

一个特性只有在下列条件全部满足时才算完成：

- 核心路径存在，不是占位。
- 行为有测试覆盖。
- 出错时有可诊断的信息。
- 文档与现实一致。
- 没有引入未记录的限制。
