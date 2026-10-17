# 兼容性与验证矩阵

**状态：在获得证据支持之前，不成立任何兼容性结论。**

只记录确实测试过的版本与操作。

| 组件 / 格式 | 版本或样本 | 操作 | 状态 | 证据 |
|---|---|---|---|---|
| .NET SDK | 10.0.402（本机） | 列出 SDK | TESTED（已测试） | `dotnet --list-sdks`，2026-10-17 |
| AssetRegistry.bin 头部 | KARDS v21 / 6.2 MB / FilterEditorOnly=1 | 读取 GUID + version | TESTED（已测试） | 十六进制核对，2026-10-17 |
| AssetRegistry.bin 全量解析（桌面 CLI） | KARDS v21 / 6.2 MB | 解析资产与 tags | TESTED（已测试） | **16250 个资产**，1.4 s；依赖节点 0、包数据 0 |
| AssetRegistry.bin 全量解析（浏览器 WASM） | KARDS v21 / 6.2 MB | 解析资产与 tags | TESTED（已测试） | **16250 个资产**，5.3 s；与桌面结果一致，UAssetAPI 在 WASM 下可用 |
| AssetRegistry.bin 语义级回写 | KARDS v21 | 写回并读回比对 | UNVERIFIED（未验证） | 依赖 UAssetRegistry 移植 |
| UAssetRegistry 库 | .NET 10 / UAssetAPI 1.1.0 | 在 Blazor WASM 下运行 | TESTED（已测试） | 编成 `Solstice.AssetRegistry` 库后在浏览器解析成功，无 AOT / 反射问题 |
| KARDS 引擎构建 | `+++UE5+Release-5.6-Fork-2352d15b` | 确认版本标签 | PARTIALLY SUPPORTED（部分支持） | 来自 usmap 文件名；带 `Fork` 字样，非纯官方 5.6 |
| .uasset 样本集 | 2137 个 / 24.5 MB | 解析包结构 | UNVERIFIED（未验证） | 样本已就位 |
| UAssetAPI | 1.1.0（上游库声明） | 加载 UAsset | UNVERIFIED（未验证） | 添加最小可执行测试 |
| UAssetAPI | TBD | 读取 FunctionExport/字节码 | UNVERIFIED（未验证） | 添加样本测试 |
| UAssetAPI | TBD | 写入修改后的 UAsset | UNVERIFIED（未验证） | 重新读取输出 |
| AssetRegistry | TBD | 解析 | UNVERIFIED（未验证） | 添加样本与结构断言 |
| AssetRegistry | TBD | 编辑并写入 | UNVERIFIED（未验证） | 往返结构测试 |
| Kismet 表达式 | TBD | 生成简单表达式 | UNVERIFIED（未验证） | 黄金样本测试 |
| 控制流 | TBD | 跳转偏移生成 | UNVERIFIED（未验证） | 序列化偏移证明 |
| 浏览器 | TBD | 文件打开/保存 | UNVERIFIED（未验证） | 浏览器集成测试 |

## 证据规则

- `PROPOSED`（提议）：仅为设计。
- `IMPLEMENTED`（已实现）：代码存在，但正确性可能未被证明。
- `TESTED`（已测试）：自动化或手工测试确实运行并通过。
- `PARTIALLY SUPPORTED`（部分支持）：只有已声明的子集经过验证。
- `UNVERIFIED`（未验证）：尚无可靠的测试证据。
- `UNSUPPORTED`（不支持）：有意不支持。

如果测试被跳过或无法运行，绝不要把它标记为通过。把环境限制与代码失败分开记录。
