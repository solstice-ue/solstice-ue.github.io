# 实现路线图

**状态：PROPOSED（提议）。日期与工作量是估计值，不是承诺。**

> **2026-10-17 重排。** 原路线图假设「从零侦察并验证 UAssetAPI」。现实是解析能力由 CCB-TEAM 既有 C# 库移植而来（见 [vision.md](vision.md) 与 [ADR 0002](decisions/0002-dual-path-bytecode.md)），因此阶段顺序改为**移植优先**：先证明既有库能在浏览器里跑，再谈 UI。

## 贯穿全项目的规则

- 每个阶段都必须能独立演示，不以「代码写完了」作为完成标准。
- 每个阶段结束都要更新 [compatibility.md](compatibility.md)，只填真正跑过的行。
- 样本一律用 [samples.md](samples.md) 里的真实文件，不用合成数据替代。
- 输入文件永不覆盖；写回一律输出新文件并读回校验。

## Phase 0 — 移植可行性评估

**目标：证明既有库能在浏览器里跑。**

任务：
- 以 git submodule 引入 UAssetRegistry 源码。
- 在 .NET 10 控制台程序中解析 `AssetRegistry.bin`（v21 / 6.2 MB），记录资产条数与耗时。
- 把同一段代码放进 Blazor WASM，验证 AOT 裁剪、反射、`System.IO` 依赖是否构成阻塞。
- 记录所有需要改造的点：`File.ReadAllBytes` / `Path.Combine` 等路径 API 改为 `Stream` / `byte[]`。

验收：
- 控制台与浏览器输出**相同的资产条数**。
- 移植改造点清单成文，每条注明「已改 / 待改 / 阻塞」。

## Phase 1 — Solstice.Web 骨架与文件进出

**目标：能在浏览器里拖进一个文件、看到它、导出它。**

任务：
- 建立 `Solstice.Web`（Blazor WASM）与 `Solstice.Core`（领域 + 工作流）两个项目。
- 拖拽单文件 → `byte[]` → 交给 Core；下载导出。
- 加载中 / 失败 / 取消三种状态。
- 配置 GitHub Pages 构建：`.nojekyll`、`<base href>`、`.wasm` MIME（见 architecture §10）。

验收：
- 拖入任意文件能看到大小与 SHA-256。
- 部署到 Pages 后行为一致。

## Phase 2 — AssetRegistry 只读列表

**目标：把 6.2 MB 的注册表在浏览器里完整列出来。**

任务：
- 接上 Phase 0 移植后的 loader。
- 表格显示 `ObjectPath` / `AssetClass` / `PackagePath`。
- 搜索过滤。

验收：
- 条目数与桌面版工具一致。
- 渲染耗时记录在案。

> 说明：编辑与详情面板**不在本阶段**，后续再加。

## Phase 3 — 新增条目与写回

**目标：往注册表里加一条，导出，再读回。**

任务：
- 「添加新项」**表单**。首期唯一用途是添加**卡牌资产**，命名规律为 `card_<类型>_<slug>`（如 `card_unit_16th_infantry_brigade_vet`、`card_event_aans`），目录为 `Cards/<国家>/<系列>/<类型>/`。表单按「国家 / 系列 / 类型 / 名字」四段收集，并实时预览完整 `ObjectPath`。
- 卡牌资产的 `AssetClass` 与 `PackagePath` 前缀**需先实测确认**，不得凭印象写死。
- 写回前显示**新增 / 修改 / 删除**清单。
- 导出 bin，并用 **CUE4Parse** 与 **UAssetRegistry** 两个独立实现分别读回比对。

验收：
- 新增一张 `card_xx_xx` 卡牌后导出，两个独立实现读回都能看到它，其余条目逐字段一致。
- 「输出字节与原文件不同」这一事实必须明确告知用户，不得隐瞒。

## Phase 4 — 资产浏览（uasset 只读）

**目标：打开 `.uasset` 并显示包结构。**

任务：
- 移植 UAssetAPI 读取路径（或直接引用上游 NuGet）。
- 显示 package summary、Imports、Exports。
- 未知类型显示为「不支持」，不猜。

验收：
- 从 2137 个样本中随机抽 50 个打开不崩溃。

## Phase 5 — 字节码查看

**目标：看到函数体。**

任务：
- 移植 KismetDecompiler 的 AST 与 CFG 结构化。
- 表达式树 + 跳转目标可视化。

验收：
- 能展开一个 `FunctionExport` 并显示表达式序列。

## Phase 6 — 最小编辑与生成（双路径）

**目标：落地 ADR 0002。**

任务：
- 既有函数：只改参数与常量值。
- 新建函数：UKAPI 从 `FunctionDescription` 生成。
- 验证「改常量是否改变序列化长度」这个前提。

验收：
- 改一个数值常量 → 写回 → 读回一致。
- 生成一个最简函数 → 读回一致。
- 运行时正确性单独标注，不混入上述结论。

## 优先级规则

先让一条链路在浏览器里真实跑通，再扩宽。

**Phase 0 不通过就不要写 UI**——如果既有库在 WASM 里跑不起来，整个「纯浏览器」前提就要重谈。
