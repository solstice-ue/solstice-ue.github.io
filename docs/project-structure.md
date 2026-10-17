# 项目结构

**状态：PROPOSED（提议）。实际结构以落地后的代码为准，本文件随之更新。**

## 1. 两个项目

刻意只从两个项目起步，避免「为了看起来完整而创建空项目」（见 `AGENTS.md`）。

| 项目 | 职责 | 依赖 |
|---|---|---|
| `Solstice.Core` | 领域模型、工作流、诊断、写回校验 | 既有解析库（submodule） |
| `Solstice.Web` | Blazor WASM UI、拖拽、状态、导出 | `Solstice.Core` |

**边界规则**：`Solstice.Core` 不得引用 Blazor；UI 逻辑不得下沉到 Core。

## 2. 目标目录布局

```
Solstice/
├─ Solstice.sln
├─ src/
│  ├─ Solstice.Core/
│  └─ Solstice.Web/
├─ tests/
│  └─ Solstice.Core.Tests/
├─ external/                    ← git submodule
│  └─ UAssetRegistry/
├─ docs/
└─ samples/                     ← 只放说明，不放样本本体
```

## 3. 引入既有库

以 git submodule 引入，允许边移植边改：

```bash
git submodule add https://github.com/CCB-TEAM/UAssetRegistry.git external/UAssetRegistry
git submodule update --init --recursive
```

`Solstice.Core` 通过 `ProjectReference` 指向 `external/UAssetRegistry/*.csproj`。

**注意**：submodule 里的改动需要单独提交到上游仓库，不要在 Solstice 里改完就当作已完成。

## 4. 构建与运行

```bash
dotnet build Solstice.slnx
dotnet run --project src/Solstice.Web
dotnet run --project tests/Solstice.Core.Tests     # 跑测试
```

环境基线：**.NET SDK 10.0.402**（已在本机验证）。

## 4.1 本机环境的三个坑（2026-10-17 实测）

这三个都会让人误以为「代码写错了」，其实是环境问题：

| 现象 | 原因 | 处理 |
|---|---|---|
| `dotnet` 报 `UnauthorizedAccessException: C:\Users\<用户>\.dotnet\...sentinel` | CLI 要写用户目录 | 设 `DOTNET_CLI_HOME` 到工作区内 |
| `NU1301 ... 安全包中没有可用的凭证` | 受限令牌下 Windows SChannel 拿不到 TLS 凭证 | 还原需在无限制权限下执行，或配置本地包源 |
| `MSB4216 无法连接到任务宿主` | Blazor 的 WASM 构建任务需要命名管道 | 构建需在无限制权限下执行 |
| `Testhost 进程已退出 ... Win32Exception (5)` | VSTest 的 testhost 无权打开父进程句柄 | **改用 xunit v3**，见下 |

### 为什么测试用 xunit v3 而不是 `dotnet test`

xunit v3 自带可执行入口（Microsoft.Testing.Platform），测试直接跑在测试程序里，不经过 VSTest 的 `testhost` 子进程，因此不受上述进程句柄限制。

```bash
dotnet run --project tests/Solstice.Core.Tests
# 输出：总计 5 / 失败 0 / 成功 5
```

不要退回 `Microsoft.NET.Test.Sdk` + `xunit`（v2）：那套在本机会直接跑不起来。


## 5. 部署（GitHub Pages）

```bash
dotnet publish src/Solstice.Web -c Release -o publish
```

发布前必须处理（详见 `docs/architecture.md` §10）：

- 加 `.nojekyll`
- 子路径部署要设 `<base href>`
- `.wasm` 的 MIME 与压缩后缀要和 `blazor.boot.json` 一致
- **不能启用 WASM 多线程**（Pages 无法设置 COOP/COEP）

## 6. 尚待确定

- 测试项目是否从一开始就独立，还是先写在 Core 里。
- 是否需要 `Solstice.Cli`（离线批量处理），当前判断是不需要——桌面版工具已经覆盖。
