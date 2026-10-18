# UKAPI 上层：字节码预览 + 平铺节点列表

**状态：`PROPOSED`（提议）—— 尚未实现。**
**记录日期：2026-10-17。**

当前阶段**只做生成（UKAPI）**，不做字节码修改。修改能力见 ADR 0002，稍后再说。

## 1. 页面布局：左右分栏

```
┌─────────────────────────┬──────────────────────────────────┐
│ 字节码预览（编辑器风格） │ 平铺节点列表                      │
│                         │                                  │
│ 000  EX_LocalVariable   │ ┌──────────────────────────────┐ │
│ 001  EX_LocalVariable   │ │ If ▸ 控制流 ●────● 真 / 假    │ │
│ 002  EX_LessEqual       │ │  条件 ◇ bool                  │ │
│ 003  EX_JumpIfNot  → 012│ └──────────────────────────────┘ │
│ 004  EX_CallMath ...    │ ┌──────────────────────────────┐ │
│ ...                     │ │ Call DamageCard               │ │
│ 012  EX_Return          │ │  in  ● card : Object          │ │
│                         │ │  in  ● amount : Int           │ │
│                         │ │  out ● result : Bool          │ │
│                         │ └──────────────────────────────┘ │
└─────────────────────────┴──────────────────────────────────┘
```

- **左**：直接展示 `EX_` 指令序列，带行号与跳转目标。这是「编译结果」的真相视图。
- **右**：每个逻辑节点是一行**宽列表项**——相当于把蓝图编辑器里的节点压成一行，而不是画在画布上。

这样「所见」与「所生成」能逐行对照：右边改一个节点，左边对应的 `EX_` 立刻可见。

## 2. 节点类型与插销

| 节点 | 控制流插销 | 数据流插销 | 编译结果 |
|---|---|---|---|
| 逻辑节点（If / 循环 / 跳转） | ✅ 有（用于跳转） | 条件/表达式 | `EX_JumpIfNot` 等 |
| **Get 节点** | ❌ 无 | ✅ 有 | **编译时自动转成变量读取**（`EX_LocalVariable` / `EX_InstanceVariable`） |
| 函数调用节点 | 视调用约定 | ✅ 有（分 in/out） | 封装的调用表达式 |

**Get 节点只存在于编辑器层**：用户看到的是「取这个值」，编译时不需要用户关心它变成什么变量。

## 3. 函数调用必须封装

调用节点不直接映射到单个 `EX_` 指令，而是编译成一个**封装结构**：

- `StackNode` —— 参数列表（按签名顺序压栈）
- 或 `FunctionName` —— 调用目标

具体用哪种、以及**不同调用之间怎么选**，由编译器按签名与 `function_flags` **推断**，不由用户在 UI 上指定：

| 线索 | 推断 |
|---|---|
| `FUNC_Static` | 静态调用，无 self |
| `FUNC_Final` | 直接调用（`EX_FinalFunction`） |
| 非 Final / 虚函数 | 虚调用（`EX_VirtualFunction`） |

## 4. 插销按类型着色，同时承担类型推断

每个数据插销带类型，颜色按类型区分（`int` / `bool` / `object` / `struct` / `enum` / `exec`）。
颜色不只是装饰——**类型推断也读同一份类型信息**：

- 连不上线的插销，说明类型不兼容，UI 直接禁止连接
- 上游未定型的表达式，从插销类型反推其输出类型

## 5. 与现有数据模型的关系

| 需要的东西 | 现在有没有 |
|---|---|
| 函数签名、参数名、in/out | ✅ `functions.json`（1,416 个 kards 函数 / 2,653 个参数） |
| 参数类型（含对象类、枚举） | ✅ `class` / `enum` 字段 |
| 类型着色所需的类型体系 | ❌ 需要新定义（Core 里还没有类型模型） |
| `EX_` 指令的显示名与操作数 | ❌ 需要一张指令表 |
| 节点 → `EX_` 的编译规则 | ❌ UKAPI 的核心，待写 |

## 6. 已确认（2026-10-17）

### 6.1 左侧字节码预览：联动编译

右栏每次编辑后**立即重新编译**，左栏同步刷新。

- 好处：所见即所得，改一个节点马上能看到它编译成哪几条 `EX_`。
- 代价：大函数每次编辑都要全量重编译。**先接受这个代价**，如果实测卡顿，
  再退化成「只重编译受影响的节点及其下游」，而不是改成手动点按钮。

### 6.2 Get 节点：先选变量范围，再提示候选

用户必须先选定范围（局部变量 / 成员变量 / 函数参数），系统才给出候选并允许选择。
不允许「什么都不选直接输入名字」——那样无法确定编译成 `EX_LocalVariable` 还是
`EX_InstanceVariable`。

## 7. 指令表与格式的来源

**不自己编操作码表。** 依据：

| 来源 | 提供什么 |
|---|---|
| [UE5 蓝图逆向 · 02 · Kismet 字节码](https://ccb-team.github.io/ue5-bp/02-bytecode.html) | `EExprToken` 操作码表、序列化格式、执行流栈四件套 |
| [11 · 常用字节码（上）](https://ccb-team.github.io/ue5-bp/11-opcodes-a.html) | 变量引用、常量、函数调用、赋值——token、操作数、渲染结果、注释原文 |
| [12 · 常用字节码（下）](https://ccb-team.github.io/ue5-bp/12-opcodes-b.html) | 控制流、执行流栈、类型转换、结构容器、委托，附**全 token 索引** |
| [09 · FKismetPropertyPointer](https://ccb-team.github.io/ue5-bp/09-property-pointer.html) | 字节码里指向属性的指针，两种形态与版本门 |
| UAssetAPI 源码 `EExprToken` | 枚举定义，用于对照 |

**注意**：那份专题的基准是 **UE 5.8**，而我们的目标是 **5.6**。操作码表在 5.6→5.8
之间可能变化（专题自己提到「UE5.8 的操作码变化」），所以落地前要按 5.6 的
UAssetAPI 枚举逐条核对，不能直接照抄。
## 8. 实测：跳转操作数是字节偏移，不是表达式索引

对 `card_brawl_test1.uasset` 的 `ExecuteUbergraph_card_brawl_test1`（56 条表达式）实测：

```
004  JumpIfNot  codeOffset=199
009  Jump       codeOffset=1433
013  Jump       codeOffset=1433
023  JumpIfNot  codeOffset=619
```

函数只有 56 条表达式，而偏移是 199 / 619 / 1433 —— **远超表达式条数**，所以它是
**序列化字节流里的字节偏移**，不是表达式数组下标。

**这直接推翻了当前 demo 编译器的实现**：`KismetCompiler` 现在把跳转目标写成指令索引，
是错的。正确做法是在序列化阶段按每条指令的**编码后字节长度**累加，得到目标地址。

## 9. EX_Context 的 Offset 是「跳过字节数」

实测 `card_brawl_test1` 的 ExecuteUbergraph：

```
[001] size=45  Context
      ObjectExpression  = EX_InstanceVariable
      Offset            = 23
      ContextExpression = EX_LocalVirtualFunction   (size = 23)
```

**`Offset` 等于 `ContextExpression.GetSize()`（23 = 23）。**

含义：对象为 null 时，执行器直接跳过 `Offset` 个字节、不做这次调用——
是**短路跳过量**，不是 vtable 偏移。

因此生成 `EX_Context` **不需要预先知道目标函数的 vtable 偏移**，
只要先构造好 `ContextExpression`、再取它的 size 填进 `Offset` 即可。
这条纠正了此前「跨对象调用靠 vtable 偏移定位」的错误结论。

## 10. UAssetAPI 没有字节码写入器

全量检索 `UAssetAPI/Kismet/Bytecode`：只有 `Read` 与 `GetSize`，**没有任何 `Write`**。

```
Bytecode 目录下 void Write( 的匹配数：0
Expressions 目录下 public override void Write 的匹配数：0
```

含义：**生成字节码的序列化器必须自己写**。UAssetAPI 只解决了「读」和「算长度」，
「写」这块是空的——这是 UKAPI 真正的工作量所在。

参照物是每个 `EX_*` 类的 `Read` 实现：读什么、按什么顺序读、什么类型，
反过来就是写法。加上 `GetSize` 可以对写出的长度做自校验。

## 11. 更正：UAssetAPI **有**字节码写入器

上一节（第 10 节）的结论是错的，原因是我 grep 时把签名写成了 `public override void Write`，
而实际返回类型是 `int`：

```csharp
public override int Write(AssetBinaryWriter writer)
{
    writer.Write(Value);
    return sizeof(int);   // 返回写出的数据字节数
}
```

**每个 `EX_*` 类都有 `Write(AssetBinaryWriter)`**，返回值是该表达式写出的字节数。
所以：

- 不需要自己写序列化器
- 构造好 `KismetExpression` 对象后，交给 UAssetAPI 的 writer 即可
- `Write` 返回的字节数可以直接用来累加、回填跳转偏移

第 10 节保留作为记录：**同一个错误（只 grep 一次就下结论）我犯了三次**
（`EX_EndOfScript`、`CityHash`、字节码写入器），根因都是没有读完就判断。

## 12. 属性指针必须构造 New（FFieldPath）形态

尝试 `new KismetPropertyPointer(new FPackageIndex(-12))` 生成 EX_LocalVariable 时抛
`NullReferenceException`。

原因：UE5.6 的资产走 `ShouldSerializeNew` 分支，序列化时读的是 `New`（FFieldPath），
而用 `FPackageIndex` 构造只会填 `Old`，`New` 仍为 null。

所以构造指针必须用 New 形态：

```csharp
new KismetPropertyPointer(new FFieldPath { Path = [...], ResolvedOwner = ... })
```

实测真实字节码里的 65 个变量表达式**全部**是 `Old=null, New=有`，与此一致。

## 13. 生成能力现状（2026-10-17）

### 已打通：读 → 生成 → 写回 → 读回验证

| 指令 | 生成 | 写回资产后读回 |
|---|---|---|
| `EX_IntConst` | ✅ | ✅ |
| `EX_LocalVariable` | ✅ | ✅ |
| `EX_Return` / `EX_EndOfScript` | ✅ | ✅ |
| `EX_FinalFunction`（带参数） | ✅ | ✅ |
| `EX_Jump` / `EX_JumpIfNot` | ✅ | 字节布局实测 |
| `EX_Let`（赋值） | ✅ | ✅ |

端到端验证过的三条链路：

1. `ExecuteUbergraph` 的 56 条表达式读出 → 写出 → 与原 `.uexp` 偏移 4217 处
   **1832 字节逐字节相同**
2. 生成 `return 42;` → 替换 → 写出 `.uasset` → 重新加载读回 2 条，值 42
3. 生成 `EqualEqual_ByteByte(7, EntryPoint)` → 写回 → 读回 `StackNode=-18`、参数 2 个
4. 生成 `Set EntryPoint = 9` → 写回 → 读回 `Let`、值 9

### 关键结构（实测）

| 结构 | 要点 |
|---|---|
| `FFieldPath` | `Path`（`FName[]`，变量名）+ `ResolvedOwner`（`FPackageIndex`，正=导出/负=导入） |
| `KismetPropertyPointer` | 必须用 `New`（`FFieldPath`）形态；`Old` 形态在 UE5.6 会 NRE |
| `EX_Let` | 需要 `Value`（指针）与 `Variable`（表达式）**同时**给 |
| `EX_JumpIfNot` | 条件表达式**内嵌**在指令里，不是数组独立项 |
| 跳转操作数 | 序列化字节流的**字节偏移**，不是表达式下标 |

### 尚未覆盖

| 项 | 影响 |
|---|---|
| `EX_Context` / `EX_LocalVirtualFunction` | 跨对象调用、虚函数调用（ubergraph 里很常见） |
| `LoadedProperties` | 生成新函数时按签名排参数顺序 |
| 新增 Import 条目 | 现在只能复用资产已有的 Import |
| `/ukapi` 页面接真实字节 | 页面目前只显示指令名；显示字节需要页面上有已加载的 `UAsset`（`AssetBinaryWriter` 需要它） |

## 14. 浏览器内解析资产的可行性

`UAsset` 的构造函数（UAssetAPI 1.1.0）：

```csharp
public UAsset(string path, ...)                        // 路径式，内部 File.Open(ReadWrite)
public UAsset(AssetBinaryReader reader, ...)           // 流式，可用于内存字节
public UAsset(EngineVersion engineVersion = ...)       // 空白资产
```

**流式构造函数存在**，所以「浏览器里从内存字节解析资产」在 API 层面是可能的。

但有两个现实问题：

1. **UE5 的资产是 `.uasset` + `.uexp` 双文件**，路径式构造函数自己拼接两半；
   流式构造函数只收一个 reader，需要自己把两半拼成单个流（或确认它支持分离模式）。
2. **`.usmap` 也是路径式**（`new Usmap(string path)`），浏览器里没有文件系统路径。

所以 `/ukapi` 接真实资产的顺序应该是：

1. 先用「拼接后的单流 + Usmap 从内存构造」做一个小验证（如果 Usmap 也支持流）
2. 验证不过，就退回到**离线模式**：本地 CLI 生成字节码，页面只负责展示与编辑节点，不直接解析资产

### 流式构造的实际签名（补充第 14 节）

```csharp
public AssetBinaryReader(Stream stream, UAsset asset = null)
public AssetBinaryReader(Stream stream, bool inLoadUexp, UAsset asset = null)
public Usmap(string path)
public Usmap(UsmapBinaryReader reader)
public UsmapBinaryReader(Stream stream, Usmap file)   // 需要 Usmap 实例
```

`AssetBinaryReader` 可以脱离 `UAsset` 构造（asset 参数可空），所以资产流式加载有戏；
但 `UsmapBinaryReader` 要求传一个 `Usmap` 实例，构造上是循环依赖，
`new Usmap(path)` 仍是唯一简单入口 —— 浏览器内没有路径，这里是真正的卡点。

## 15. EX_Context 接入编译器的前置约束

`EX_Context.Offset` 必须等于 `ContextExpression.GetSize(asset)`，而 `GetSize` **需要一个 `UAsset` 实例**。

但 `KismetCompiler.CompileToExpressions` 目前是无状态的纯函数（只接收节点与索引，不碰资产）。
要让 `EX_Context` 进入编译器，必须二选一：

| 方案 | 说明 |
|---|---|
| 给编译器传入 `UAsset` | 直接调 `GetSize`，最准确；但 Core 会依赖资产实例，降低可测试性 |
| 传入「计算 size 的回调」 | `Func<KismetExpression, uint>`，由调用方（CLI / 测试）提供，保持 Core 无状态 |

**倾向方案二**：编译器仍然不持有资产，但能拿到准确的长度。

这也解释了为什么 `EX_Context` 目前只是「能构造、能往返」，还没接进 `CompileToExpressions`。

## 16. CompileToExpressions 的节点覆盖（实测）

`CompileToExpressions` 目前只处理四种节点：

```
AssignNode / CallNode / ContextCallNode / ReturnNode
```

**`IfNode` 不在其中** —— 它只在旧的 `Compile`（输出 `KismetInstruction[]` 给人看的那条路径）
里被处理。所以：

| 路径 | 支持 If |
|---|---|
| `Compile` → 指令列表（预览用） | ✅ |
| `CompileToExpressions` → 可序列化字节码 | ❌ |

要让条件分支真正可生成，需要给 `CompileToExpressions` 补 `IfNode`：
生成 `EX_JumpIfNot` 时**跳转偏移要按字节算**（需要 measure 回调 + 两遍扫描回填），
不能沿用 `Compile` 里那套「按指令索引」的简化算法。
