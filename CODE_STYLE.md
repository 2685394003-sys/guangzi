# Guangzi 代码规范 v1

> **这份文件就是规范本体。改这里 = 改规范。**
> 配套三个检查器：`.editorconfig`（IDE 自动格式化）、`ArchitectureTests`（跑测试时校验）、`style_rules.txt`（规则开关）。
> 最后更新：2026-11-11

---

## 0. 一句话原则

规范的目的不是好看，是**让错误在写的时候就暴露**。
所以只写两种规则：**能自动检查的**、**违反了一定出 bug 的**。其余一律不写——写多了没人看。

---

## 1. 分层（硬约束，测试会红）

| 层 | 目录 | 允许引用 | 绝对禁止 |
|:--|:--|:--|:--|
| **Core** | `Core/` | 仅 .NET BCL（`System.*`） | `UnityEngine`、`UnityEditor`、`Unity.InputSystem`、任何第三方 |
| **Runtime** | `Runtime/` | Core + UnityEngine + InputSystem | `UnityEditor`（例外见下） |
| **Tests** | `Tests/` | Core + NUnit + UnityEngine（读路径用） | 生产代码反向引用 Tests |
| **Editor** | `Editor/` | 全部 + UnityEditor | — |

**唯一的例外**：`#if UNITY_EDITOR ... #endif` 包裹的 `UnityEditor` 调用是**允许的**，因为打包时这段代码根本不参与编译（`Application.Quit` 的编辑器分支就是这么写的）。检查器会先把整块剥掉再检查。

**依赖方向只能是 `Runtime → Core`，不能反向。**
Core 里出现一个 `UnityEngine` 字母，测试就失败。这不是洁癖——Core 能脱离 Unity 跑单元测试，改规则的迭代速度差十倍。

---

## 2. 命名

| 元素 | 规则 | 示例 | 反例 |
|:--|:--|:--|:--|
| 类 / 结构体 / 枚举 | PascalCase | `GridMap` | `gridMap` |
| 接口 | `I` + PascalCase | `IResourceStore` | `ResourceStoreInterface` |
| 方法 | PascalCase | `TryPlace` | `tryPlace` |
| 属性 | PascalCase | `CellSize` | `cellSize` |
| **纯私有字段** | `_` + camelCase | `_cells` | `cells` |
| **`[SerializeField]` 字段** | camelCase，**不带下划线** | `pitch`、`cellSize` | `_pitch` |
| 常量 | PascalCase | `WoodWall` | `WOOD_WALL` |
| 参数 / 局部变量 | camelCase | `origin` | `Origin` |
| 布尔 | `is` / `has` / `can` / `should` 开头 | `isPassable` | `passable2` |
| 事件 | PascalCase 过去式 | `ObjectPlaced` | `OnPlaced` |
| 枚举值 | PascalCase | `BuildInvalidReason.Occupied` | `OCCUPIED` |

**为什么私有字段带下划线、序列化字段不带**：
`[SerializeField]` 的字段名会直接显示在 Inspector 上，`pitch` 比 `_pitch` 好看，Unity 官方模板也这么写。
纯内部字段带下划线，是为了和同名参数一眼分开——`_cells = cells` 这种写法一眼就知道谁赋给谁。

---

## 3. 文件与类型

| 规则 | 说明 |
|:--|:--|
| **一个文件一个 public 类型** | 文件名 = 主类型名 |
| **同族值类型可共生** | 一个文件不超过 **3** 个类型（阈值在 `style_rules.txt` 里改） |
| 目录按**职责**分 | `Grid/` `Build/` `Save/`，不是 `Classes/` `Structs/` |
| 每个 asmdef 目录自带 `.asmdef` | 不允许裸文件堆在 `Scripts/` 根 |

**曾经超限、现已清**：`Core/Build/` 原来把 `ResourceAmount` / `BuildDef` / `PlacedObject` / `BuildCatalog` 四个类型塞在 `BuildDef.cs` 一个文件里，2026-11-11 已按类型拆成四个文件。

---

## 4. 注释

| 规则 | 说明 |
|:--|:--|
| **public 类型 / 方法必须有 `///` 摘要** | 一句话说清"它负责什么" |
| **写为什么，不写是什么** | `// 遍历所有格子` 是废话；`// 先查全部格子再落地，避免半成品状态` 才有价值 |
| **术语首次出现中英双语** | 预制体(prefab)、碰撞体(collider)、实例化(Instantiate) |
| 禁止注释掉代码 | 用 git 回退，别留尸体 |
| 禁止 `#region` | 需要折叠说明这个类太大了 |

---

## 5. 代码风格（`.editorconfig` 自动管，不用记）

| 项 | 规则 |
|:--|:--|
| 缩进 | 4 空格，不用 Tab |
| 大括号 | 换行（Allman） |
| 行宽 | 120 |
| `var` | 只在右侧类型一目了然时用 |
| 单表达式 | 可用 `=>` 表达式体 |
| 编码 | UTF-8，CRLF |

---

## 6. 防御性规则（写代码时强制）

| 规则 | 理由 |
|:--|:--|
| `public` 方法入口校验参数（null / 越界），非法就抛异常 | 失败要早、要响 |
| **禁止用异常做流程控制** | 建造失败返回 `BuildCheck`，不抛异常——异常是给 bug 用的，不是给玩家操作失误用的 |
| 集合返回 `IReadOnlyList` / 只读视图 | 别把内部数组暴露出去让人改坏 |
| `MonoBehaviour` 不写构造函数 | 初始化放 `Awake` |
| 事件订阅必须在 `OnDestroy` / `OnDisable` 解绑 | 防泄漏，Unity 里最常见的坑 |

---

## 7. 检查机制（三档，都是零依赖）

| 档 | 文件 | 怎么触发 | 管什么 |
|:--|:--|:--|:--|
| **1** | `.editorconfig`（项目根） | IDE 保存 / `Ctrl+K Ctrl+D` 格式化 | 缩进、括号、空格、行宽 |
| **2** | `Tests/ArchitectureTests.cs` | Unity Test Runner 点 Run / 我这边跑 | 分层引用、`#region`、单文件类型数、public 注释覆盖 |
| **3** | `Tests/style_rules.txt` | 被档 2 读取 | **规则开关**，改它不用碰代码 |

**档 2 是核心：规范变成会失败的测试。** 谁违反就红，改不掉就进不了主干。

---

## 8. 怎么改规范

| 想改什么 | 改哪里 |
|:--|:--|
| 缩进、括号、空格 | 只改 `.editorconfig` |
| 单文件最多几个类型、哪些命名空间禁引用、要不要禁 `#region` | 只改 `Tests/style_rules.txt` |
| 命名规则本身 | 改本文 §2 + `ArchitectureTests.cs` 对应断言 |
| 分层结构 | 改本文 §1 + `Guangzi.*.asmdef` 的 `references` |

改完跑一次 `ArchitectureTests`，绿的就算改成功。

---

## 9. 现在这套规范管不到的（诚实说明）

| 缺口 | 现状 | 要不要补 |
|:--|:--|:--|
| 命名规则**没有自动检查** | 靠 code review 和 `.editorconfig` 的 IDE 提示（波浪线，不阻断） | 要真正的强制检查需要装 Roslyn 分析器（StyleCop），**涉及装包，我会先问你** |
| 循环复杂度、方法长度 | 无检查 | 人少的时候不需要，代码量上来再加 |
| 测试覆盖率 | 无统计 | 首版不需要，靠"新功能必须带测试"的人工纪律 |
| 与 IDE 的联动 | VS 2026 会读 `.editorconfig`，但不读 `style_rules.txt` | 正常，后者只给测试用 |
