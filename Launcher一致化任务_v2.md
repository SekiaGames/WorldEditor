# Launcher 一致化任务 v2：客户端模拟 + Godot 表现 + 配置进 C#

> 自包含文档。只读这一份就能接手并继续推进。
> 上一版（逐文件适配循环）见 `Launcher一致化任务.md`，其历史轮次见 `Launcher字节一致化路线.md`，差异清单见 `Launcher与Open-Warcraft3的差异_v2.md`。
> 基准：Launcher 511 个 cs / 5.9 万行，客户端 `test/Open-Warcraft3` 1,505 个 cs / 29.6 万行（不含 Android 与工具）。
> 写于 2026-11-13。

---

## 0. 方向

三件事定死：

1. **逻辑用客户端源码**。模拟、地图解析、JASS 原样搬进来，逐字节一致。
2. **画面用 Godot 实现**。地形、模型、特效、界面、音频都由 Godot 节点与资源体系画。
3. **配置编译进 C#**。`extract.pck` 只放美术资产（模型、贴图、音频、界面图）。所有表格与配置——slk、txt、Misc、光照曲线、界面描述——一律由 Extract 生成为 C# 源码，编进程序集。

第 3 条带来一个关键决定：**生成期跑客户端的加载流水线，运行期只装成品**。客户端加载期是「解析 slk/txt → 多次合并 → 变体回填 → 吸收地图覆盖」的一条链，运行期重写这条链必然走样。做法是在 Extract 仓库里放一个生成器，引用客户端工程，用真实的 War3 数据跑完整条链，把结果 cells 导出成 C#；Launcher 启动时直接装 cells，一行合并逻辑都不写。

交换的东西：

| | v1（逐文件适配） | v2（本文件） |
|---|---|---|
| 逻辑 | Launcher 自写等价实现 | 客户端源码整目录搬入 |
| 画面 | Launcher 自写或补客户端文件 | Godot 重写，按客户端逐项对齐参数 |
| 配置 | 运行时读表 | 生成 C#，编译进程序集 |
| 资源 | 混用 | pck 只放美术 |
| 达标单位 | 单个 .cs | 整个模拟层 + 整个数据层 |

画面这块要说清楚代价：渲染代码不共享，所以「画面一致」靠资源同源、参数对齐、逐项截图验收。§5.4 给出 20 项清单，清单没关完，SSIM 达线也不算过。

---

## 1. 目标与判据

Launcher 在**地图加载界面**和**游戏运行全程**的画面与行为与客户端一致。五条判据：

| 判据 | 定义 | 达标线 | 性质 |
|---|---|---|---|
| 逻辑同源 | 与客户端逐字节相同的模拟层 .cs | ≥ 99%（只允许附录 A 的补丁） | 硬指标 |
| 逻辑等价 | 同图、同种子、同输入下逐 tick 状态摘要 | 3000 tick 逐 tick 一致 | 硬指标 |
| 数据等价 | 生成表与客户端运行时加载结果的规范化哈希 | 完全相等 | 硬指标 |
| 画面等价 | 同机位、同 tick 截图 | SSIM ≥ 0.99，均值通道差 ≤ 2/255，无结构性缺失 | 需人工看图确认 |
| 加载等价 | 加载阶段序列、界面元素、文本、进度 | 阶段序列一致，界面截图达画面等价 | 硬指标 + 截图 |

**画面差异按 §5.4 清单逐项关掉，不按「整体看起来像不像」判断。**

---

## 2. 现状

### 2.1 规模

| 区域 | 客户端 | Launcher |
|---|---:|---:|
| 数据层（`L1_Adapter/Formats` + `L2_Core`） | 17,880 | 5,691 |
| 模拟层（`Src/` + `L3_Simulation`） | 141,749 | 30,741 |
| 表现层（`L4_Composition/Game`） | 96,507 | 20,693 |
| 平台层 | 9,226 | 2,086 |
| 合计 | 265,362 | 59,211 |

Launcher 表现层分布：`Scenes/War3GameScene` 9,276、`Diagnostics` 6,261、`Environment` 1,927、`UI` 1,691、`Presentation` 795、`Audio` 472、`Rendering` 196、`Camera` 75。

### 2.2 逐文件适配走到了哪

| 指标 | 值 |
|---|---:|
| 同路径 .cs | 262 |
| 其中字节一致 | 112 |
| 差 1 到 10 行的文件 | 1 |
| 差 81 行以上的文件 | 119 |

112 是那条路线的上限。剩下的差距都是子系统级。

### 2.3 四个实测结论

**一、客户端核心源码在 net8.0 下能编译，只有 2 处需要补丁。**

Godot 4.7.2 的 `GodotSharp.dll` 只有 `lib/net8.0`，随包 `GodotPlugins.runtimeconfig.json` 声明 net8.0；本机装了 8.0.20，进程落在 net8 运行时，net11 程序集载不进去。所以 Launcher 锁 net8，客户端跑 net11.0-rc。

把客户端 Formats + Core + Simulation（15.96 万行，含 3.3 万行寻路）单独抽出来按 net8.0 编：

| 阶段 | 错误数 | 性质 |
|---|---:|---|
| 原始 | 6,444 | 缺 ImplicitUsings 与包引用 |
| 补隐式 using | 158 | 全部是缺 NuGet 包 |
| 补包引用 | 2 | net9 API：`Dictionary.Capacity`、`PersistedAssemblyBuilder` |

两处都在 JASS 代码里，改法见附录 A。War3Net 三个 csproj 的 TFM 从 net9.0 改 net8.0，并给 `War3Net.IO.Mpq` 补一条到 `War3Net.Common` 的直接引用。

**二、客户端的数据装载是一条链，不是一个函数。**

`War3ObjectData.Load` 依次做：5 张单位 slk 合并 → 6 族 txt（Func + Strings）合并 → 技能/物品/装饰物/科技 slk 与 txt → `MergeAbilityText` 特殊合并 → `BackfillCodeVariants` 变体回填（Abu5 这类变体行要从 code 基行继承未覆盖字段）→ 地图物编覆盖（w3u/w3a/…）。中间还夹着 `authoritative` 标记、`_` 空值与空串清空的不同语义。

运行期重写这条链等于重写数据层。所以方案是：生成期用客户端自己跑一遍，运行期只装成品（§4.3）。

**三、模拟层已经为外部表现层留了契约。**

| 契约 | 位置 | 内容 |
|---|---|---|
| 特效 | `Src/Core/Effect.cs` | `VisualModelPath`、`VisualTarget`、`VisualAttachmentPoint`、`VisualHeight`、`VisualScale`、`VisualAgeSeconds`、`VKind`、`VRadius` |
| 弹道 | `Src/Core/MissileEffect.cs` | `TravelProgress`、`ArcOffset`、`SegmentLaunchPos`、`SegmentTargetPos`、`LaunchHeight`、`ImpactHeight`、`Direction`、`Team`、`MissileArt` |
| 单位缩放 | `Src/Units/Unit.cs:172-181` | `VisualScale`、`VisualScaleX/Y/Z`、脚本缩放 |

Godot 视图按这些字段画，模拟层不为渲染改一行。

**四、数据结构对生成友好。**

`War3TypeData` 的字段容器是公开的：

```csharp
public class War3TypeData : IRuntimeObjectData
{
    public string Id { get; internal set; }
    public string BaseId { get; internal set; }
    public readonly Dictionary<string, string> Fields;          // 普通字段
    public readonly Dictionary<War3ObjectFieldKey, string> LevelFields;  // Data1/DataA 这类分级字段
}
```

生成代码可以直接装 `Fields` / `LevelFields`，不需要走 slk 文本解析。`War3ObjectData` 是 `sealed`，加 `partial` 后由 Launcher 侧补一个 `LoadGenerated`，客户端原有方法一个字不改。

---

## 3. 架构

### 3.1 分层

```
Launcher（Godot 4.7.2 / net8.0）
├── Client/    客户端模拟、数据与生成表，禁止 using Godot
│      L1_Adapter/Formats      地图段解析、txt 解析、物编覆盖
│      L2_Core                 配置模型
│      L3_Simulation           World / Unit / Ability / Buff / Order / Pathing / JASS
│      Src/                    Core Abilities Components Skills Units Orders Items Events
│      Data/Generated/         由 --data-codegen 生成的 C# 表（并非客户端源码）
├── Bridge/    客户端状态 → 视图快照；输入 → 命令；坐标与资源路径换算
├── View/      Godot 表现层（Terrain / Doodads / Units / Effects / Fog / Camera / Hud / Audio）
└── Platform/  入口、pck 挂载、启动参数
```

依赖方向：`View → Bridge → Client`。合一个程序集之后，这两条不能再靠工程引用强制，改由两条规矩守：`Client/` 里不写 `using Godot`，视图层不直接摸模拟类型。审查靠 `tools/check-layers.sh` 扫一遍，扫到就报。

### 3.2 工程：两个单程序集

两条硬约束：Launcher 一个工程、Extract 一个工程，都不再拆子工程。

| 工程 | 内容 |
|---|---|
| `Launcher.csproj`（Godot.NET.Sdk） | 客户端源码 + War3Net 源码 + Bridge + 视图 + 平台 + 两个开发模式（`--data-probe`、`--data-codegen`） |
| `Extract.csproj`（Godot.NET.Sdk） | 资源转换，保持原样；不再承载任何数据生成器 |

原来计划的三个 csproj（Client / View / Launcher）与两个工具工程（DataProbe / DataCodegen）全部取消。分层靠目录与 `Compile Remove` 白名单约束，不靠程序集边界。

**代价**：客户端源码与 Launcher 自有代码进同一个程序集，同名类型会撞。Launcher 现有 `Src/`（18,475 行）与 `GameCore/L1` `L2` `L3`（17,957 行）里有一整套与客户端同名的 `World`、`Unit`、`War3GameplayConfig`，必须先删，否则 CS0101 满屏。

**收益**：没有程序集边界要维护，也不会有「A 工程的改动没同步到 B」的问题；Godot 只需要加载一个 dll。

### 3.3 客户端源码的三类处理

| 类别 | 处理 | 数量 |
|---|---|---:|
| verbatim | 逐字节复制 | 除下面两类之外全部 |
| patch | 最小区分，登记附录 A | 3 处（2 处 net8、1 处 `partial` 声明） |
| exclude | 不搬 | `L4_Android`、`MobileMapEditor`、`Tool`、`VisualStudio`、`L4/Game/Imgui`、`L1_Adapter/RHI.OpenGL`、`L4/Game` 的渲染与界面 |

同步脚本 `tools/sync-client.sh`：rsync 到 `Client/`，随后跑三条校验（补丁清单外逐字节一致、无 `using Godot`、不引用 View 与 Launcher）。生成表放在 `Client/Data/Generated`，同步不动它。

### 3.4 范围

| 客户端模块 | 处理 |
|---|---|
| `Src/**`、`L1_Adapter/Formats`、`L2_Core`、`L3_Simulation/**` | 搬 |
| `L4/Game/Rendering`、`Environment`、`UI`、`Scenes`、`Input`、`Audio` | 不搬，Godot 重写 |
| `L4/Android`、`Tool`、`MobileMapEditor`、`Imgui` | 不搬 |
| Glue 层（主菜单、地图选择、战役） | 不搬 |

范围内：进图、加载、对战、结算的全部逻辑与画面。

---

### 3.5 单程序集迁移顺序

合并要先过一道账：删掉自有模拟层后，视图层有 174 处引用直接失效（`W3eTerrain` 70、`UnitType` 30、`Item` 16、`SlkTable` 12 等），另有 1,277 处引用了与客户端同名的 `Unit`/`World`，不会报错但语义不同。前者是编译错误，后者是静默错。按下面五步走，每一步都能编译。

| 步 | 动作 | 结果 |
|---|---|---|
| 1 | `Launcher.csproj` 内联 Client 与 War3Net 的源码；删自有 `Src/`、`GameCore/L1` `L2` `L3`，`GameCore/L4` 移出编译 | 编译绿。Launcher 只剩入口与平台壳，无画面 |
| 2 | 抹平三个已知坑（见下） | 编译绿，错误数归零 |
| 3 | 把 `DataProbe`、`DataCodegen` 改成 Launcher 的两个命令行模式，删掉两个工具工程与 `Extract/tools` | 数据生成与校验回到单程序集内 |
| 4 | 接 `GameSession` + JASS，跑通 `--state-digest` | P1 达标，逻辑层可用（headless） |
| 5 | 按 §5.4 清单新写 `View/`：地形 → 装饰物 → 相机 → 单位 → 特效 → 界面 → 音频 | 每完成一项关掉对应清单项 |

第 1 步之后 Launcher 没有画面，直到第 5 步的地形项落地。这是既定代价，`--data-probe`、`--state-digest`、`--headless` 不受影响。

**旧视图不能"加回编译"，只能当参考**

原以为视图层是"改 174 处类型引用"就能回来，实测推翻了这个判断。真正的规模在另一个方向：

| 项 | 数量 |
|---|---:|
| L4 里出现 `Unit` 的文件 / 次数 | 44 个文件 / 994 次 |
| L4 里出现 `World` 的次数 | 283 次 |

`Unit` 与 `World` 在客户端也有同名类型，所以这 1,277 处**不会报错，会静默绑定到客户端类型上**，行为完全不同。v1 已经踩过三次（`World.Tick`、`Learned`、`Classifications`），这里是同一类问题放大一千倍。

结论：旧 `GameCore/L4_Composition` 整体不进新程序集。它的价值是算法与参数（§5.2 的参数表、§5.4 的实现要点），代码本身按视图重写。留在磁盘上当参考，不参与编译。

**删什么、留什么**

| 目录 | 处置 | 依据 |
|---|---|---|
| `Src/`（18,475 行） | 删 | 与客户端同名同义，全部由 Client 取代 |
| `GameCore/L1` `L2` `L3`（17,957 行） | 删 | 同上；含 Launcher 自有的 w3x/slk 解析器，客户端有等价实现 |
| `GameCore/L4_Composition`（20,693 行） | 移出编译，作参考 | 994 + 283 处同名引用，不能沿用 |
| `Platform/Godot/Extensions/MapDocument.cs`、`GmapPackage.cs` | 删（gmap 容器 P5 再议） | 解析职责归客户端 |
| `Platform/Godot/Assets/`（pck 路径解析、资源查找） | 留 | 视图要用，客户端没有对应物 |
| `Platform/Godot/App/LauncherApp.cs` | 留骨架，重写为薄 host | 命令行、主循环、模式分发 |

合并完成后 Launcher 自有代码约 2,000 行：入口 + pck 挂载 + 参数解析 + 两个开发模式，其余全是客户端源码。

**三个已知坑**

| 坑 | 现象 | 解法 |
|---|---|---|
| War3Net.Common 的 `global using System.Drawing;` | 内联后这条全局 using 作用于整个程序集，视图里的 `Image`、`Color` 在 `Godot` 与 `System.Drawing` 之间歧义，82 处 | 内联时去掉这一行（War3Net 实际只用到 `Point`/`Size` 少数类型，缺就补显式 using） |
| War3Net 三个工程的 `obj/` 被 glob 扫进来 | 106 处 CS0579 重复程序集特性 | `Compile Include` 时排除 `**/obj/**`、`**/bin/**` |
| Launcher 与客户端同名类型 | CS0101 与语义漂移 | 自有 `Src/`、`GameCore/L1` `L2` `L3` 直接删除，不做兼容层 |

## 4. 数据

### 4.1 四类数据，四条路

| 数据 | 例子 | 落地方式 | 运行期从哪来 |
|---|---|---|---|
| 美术资产 | `.tscn`、`.tres`、`.png`、`.glb`、`.wav`、`.mp3` | Godot 资源，打进 `extract.pck` | Godot 加载 |
| 表格 | `UnitData.slk`、`AbilityData.slk`、各 `*Func.txt`/`*Strings.txt` | 生成 C# 表（成品 cells） | 直接装对象 |
| 文本配置 | `MiscData.txt`、`MiscGame.txt`、`MetaHero.txt`、`UnitMetaData.slk`、`common.j`、`Blizzard.j`、`*.ai`、FDF | 生成 C# 常量 | 经 `IWarcraftFileSource` 供给 |
| 地图数据 | `war3map.w3e/.w3i/.doo/.w3u/.w3a/.j/.wts/.wpm` | 不生成，随地图走 | 运行时从 w3x 读 |

第四类是玩家自己的地图，天然不能预生成；它由客户端的 `War3Map*` 解析链和 `MergeMapObjectData` 处理，链路保持原样。

### 4.2 pck 里放什么

只放美术。当前 `extract.pck` 988 MB 里已经具备的部分：

| 类型 | 数量 | 用途 |
|---|---:|---|
| .tscn | 6,946 | 模型 |
| .tres | 25,980 | 材质 |
| .png | 6,380 | 贴图 |
| .glb | 3,264 | 网格 |
| .wav / .mp3 | 6,357 | 音频 |

要搬出 pck 的有三类：`Tables/*.json`（63 个）、`Texts/*.json`（137 个）、`.fdf`（84 个）。它们都是配置，转成 C# 之后从 pck 删除。

`*.wc3.json`（3,264 个）Launcher 不需要。它的内容是动画序列表、包围盒与 `boundsRadius`，而转换后的模型场景已经自带这些：序列在 `*.anim.tres`（`AnimationLibrary`，键就是 MDX 原名 `Stand - 1`、`Attack - 1`、`Walk`），附着点在 `BoneAttachment3D` 节点（`attach_Weapon_Ref` 这类），包围盒 Godot 能直接从网格算。

它还剩一个消费者：Wiki 的 `Data.cs:881`（`Wc3JsonFor` → `MaterialDetailAlpha`，第二层材质的细节透明）。文件缺失时返回 null，调用点降级跳过，不会崩。所以是否停发取决于 Wiki：保留这个效果就继续生成但不进 pck，放弃就整体停发。

**资源目录整理**：`assets/common/mesh/particle_quad.tres` 已移到 `assets/mesh/particle_quad.tres`，生成器常量与 3,682 个生成场景的引用同步改完。`assets/common/lighting/dnc.json` 在 §4.6 转成 C# 之后，整个 `assets/common` 目录即可删除。

### 4.3 表格怎么变成 C#

**生成器位置**：`Launcher` 的 `--data-codegen` 模式，代码在 `Client/Data/DataCodegen.cs`，由 `LauncherApp` 解析参数后调用。

**为什么在 Launcher 而不是 Extract**：内联之后客户端源码就在 Launcher 程序集里，`War3ObjectData.Load` 可以直接调，不需要引用任何外部工程。Extract 是 net8 的 Godot 工程，引用不了 net11 的客户端，把生成器放进去只能再开子工程——与单程序集约束冲突。生成期才需要安装目录，运行期不需要。

**流程**：

```
1. Load(安装目录)                       ← 客户端自己的代码，整条链跑完
2. 遍历 Units / Abilities / Buffs / Items / Destructables / Upgrades
3. 每个对象输出：rawcode、BaseId、Fields、LevelFields
4. 写 Client/Data/Generated/*.g.cs
5. 计算规范化哈希，写进 Generated/DataDigest.g.cs
```

**生成格式**（一行一个对象，字段用 `\t` 分隔，避免 81 万个字段各占一行）：

```csharp
namespace OpenWC3.Game.Generated;

static class UnitsTable
{
    // rawcode \t field=value \t field=value ...
    internal static readonly string[] Rows =
    {
        "hfoo\tName=步兵\thp=420\t...
    };
}
```

分级字段写成 `DataA1=1.5` 这种列名形式，装载时交给客户端现成的 `SetField` 处理列名到 `(Field, Level, Pointer)` 的映射，避免自己实现一套解析。

**装载**：`Client/Data/Generated/War3ObjectData.Generated.cs`

```csharp
public sealed partial class War3ObjectData
{
    public static War3ObjectData LoadGenerated(string war3Dir, string mapPath)
    {
        var data = new War3ObjectData();
        data.ApplyUnitRows(UnitsTable.Rows);                 // 内部：GetOrCreate + SetField
        data.ApplyTypedRows(_abilities,     AbilitiesTable.Rows);
        data.ApplyTypedRows(_buffs,         BuffsTable.Rows);
        data.ApplyTypedRows(_items,         ItemsTable.Rows);
        data.ApplyTypedRows(_destructables, DestructablesTable.Rows);
        data.ApplyTypedRows(_upgrades,      UpgradesTable.Rows);
        using var resources = new War3ResourceManager(war3Dir, mapPath,
            edition: War3GameEdition.TheFrozenThrone);
        data.MergeMapObjectData(war3Dir, mapPath, resources: resources);
        data.CloseStockResearchLists();
        return data;
    }
}
```

`ApplyUnitRows` / `ApplyTypedRows` 是 Launcher 侧新增的两个私有方法，逐行 `SetField` 写入。用到的都是类内现成成员：`GetOrCreate`、`_abilities` 等字典、`War3TypeData.SetField`（`internal`）、`MergeMapObjectData`（private）。partial 文件在同一程序集内，访问没有问题。

`War3ObjectData` 的声明加 `partial`，是附录 A 的第 3 处补丁。

**体积与预算**（估自 Extract 现有 json：63 张表、22,751 行、810,794 单元格）：

| 项 | 预算 |
|---|---|
| 生成源码总量 | ≤ 6 MB |
| 文件数 | 每表一个，约 15 个 |
| 增量编译时间 | ≤ 30 s |
| 启动装载 | ≤ 50 ms |

超预算的处置顺序：先按表拆文件，再把单表按首字母分片，最后才考虑紧凑编码。

**正确性门禁**：生成器 dump 时算一份 `sha256`（按 rawcode 排序，字段名排序，值原样，`LevelFields` 一并纳入），Launcher 的 `--data-probe` 装载后再算一次，两边必须相等。这条把「生成是否等价」变成机器可判，不靠人盯。

### 4.4 文本配置怎么走 C#

文本类配置保持客户端原有的解析与合并逻辑，只换来源。

**接口**：`War3ResourceManager` 加一个文件源回退（附录 A4）：

```csharp
public interface IWarcraftFileSource
{
    bool Exists(string path);
    bool TryReadBytes(string path, out byte[] bytes);
    bool TryReadText(string path, out string text);
}
```

查档案失败时问它。这个回退挂成静态（启动时设一次），`War3GameplayConfig.Load`、`JassRuntimeHost`、地图读取各自构造的 `War3ResourceManager` 都会自动吃到，不必逐个传参。Launcher 的实现直接返回生成的 C# 常量：

| 文件 | 生成物 |
|---|---|
| `Units\MiscData.txt`、`MiscGame.txt`、`MetaHero.txt`、`UI\MiscData.txt` | `MiscTexts.g.cs` |
| `Units\UnitMetaData.slk`、`AbilityMetaData.slk`、`ItemMetaData.slk` | `MetaTables.g.cs` |
| `Scripts\common.j`、`Scripts\Blizzard.j` | `JassScripts.g.cs` |
| `scripts\common.ai`、`human.ai`、`orc.ai`、`undead.ai`、`nightelf.ai`、`campaign.ai` | `AiScripts.g.cs` |
| 84 个 `.fdf` | `FdfSources.g.cs`（P4 用） |

JASS 源码必须按字节保真（客户端注释里写明了：有地图把一个汉字的每个字节拆进独立字符串字面量）。生成时用 `\uXXXX` 转义或可空字节映射，加载后仍是同一串字节。

**地图覆盖优先**：`war3mapMisc.txt`、`war3mapSkin.txt` 在地图包里，仍由地图档案读到，回退源不参与，保证地图配置优先于生成默认值。

### 4.5 地图数据

运行时打开 `.w3x`（War3Net 读 MPQ，不需要安装目录），走客户端 `War3Map*` 解析链。地图里的物编覆盖（`war3map.w3u` 等）在生成表之上合并，用的还是客户端 `MergeMapObjectData`。

`.gmap` 的定位：P1 起 Launcher 直接吃 `.w3x`；`.gmap` 的导入导出继续由 Map 工程负责，P5 再决定是否做 gmap → w3x 的内存转换。

**一个必须显式处理的默认值**：`War3ResourceManager` 用「War3x.mpq 是否存在」判定版本，没有游戏档案时会把 `Edition` 落成 RoC，平衡表与行为都会跟着变。Launcher 构造时显式传 `War3GameEdition.TheFrozenThrone`，`LoadGenerated` 与地图读取用同一个值。

### 4.6 光照这类烘焙配置

`Extract/assets/common/lighting/dnc.json` 是从游戏 `Environment\DNC` 烘焙出来的 24 小时曲线，每个环境一套，每 0.5 小时一个 keyframe，含太阳方向、太阳色、强度、环境色、雾色。它现在只被 Wiki 读取，Launcher 完全没有光照。

按「配置走 C#」这条，`LightingExportCli` 在写 json 的同时生成 `Lighting.g.cs`：

```csharp
static class LightingTable
{
    // family \t 0,-0.675456,-0.405856,0.615662,0.235294,0.4,0.6,1.3,...（时间,方向xyz,太阳色,强度,环境色）
    internal static readonly string[] Terrain = { "DNCLordaeron\t0,..." };
    internal static readonly string[] Unit = { "DNCLordaeron\t0,..." };
}
```

`View/Lighting` 按当前时间插值 keyframe，写入 `WorldEnvironment` 与方向光。

Launcher 只吃 `Lighting.g.cs`，不读 json。json 留给 Wiki（`Wiki/src/Lighting.cs:15` 读它）；Wiki 迁到同一份生成的 C# 之后，`assets/common/lighting/dnc.json` 与 `assets/common` 目录一起删除。两边由同一个解析器产出，迁移期间不会漂。

### 4.7 资源路径映射

| WC3 | pck |
|---|---|
| `Units\Human\Footman\Footman.mdx` | `res://extract/Units/Human/Footman/Footman.tscn` |
| `Units\Human\Footman\Footman.blp` | `res://extract/Units/Human/Footman/Footman.png` |
| `Sound\Units\...wav` | `res://extract/Sound/...wav` |

规则是替换分隔符、按扩展名换后缀。例外（`SharedModels`、`ReplaceableTextures`、`UI`）写进 `Bridge/AssetPath.cs` 的例外表。找不到的资源必须打日志并由 `--asset-probe` 列出。

---

## 5. 表现层

### 5.1 三层职责

| 层 | 职责 | 不该做的事 |
|---|---|---|
| 模拟（Client） | 判定、结算、状态推进、JASS | 不碰节点、不碰贴图 |
| 桥（Bridge） | 每帧压快照；输入翻成 `PlayerCommand` | 不做插值、不持有资源 |
| 视图（View） | 节点、动画、材质、界面、相机、音频 | 不读 `Unit` 字段、不改模拟状态 |

快照内容：

| 视图 | 字段 |
|---|---|
| UnitView | id、类型码、玩家、队伍色、位置、朝向、缩放、HP/MP 比例、建筑/英雄/幻象标记、隐身态、动画状态与当前序列 |
| EffectView | 模型路径、位置、附着单位、附着点、高度、缩放、存活比 |
| MissileView | 起点、终点、进度、弧度、美术路径、队伍 |
| PlayerView | 金、木、人口、人口上限、是否本地玩家 |

### 5.2 视觉参数集中登记

所有画面常量集中在 `View/ViewTuning.cs`，每个值注明客户端出处，代码里禁止裸数字。

| 参数 | 客户端出处 |
|---|---|
| 相机距离、俯角、FOV、边缘滚屏宽度 | `Camera/War3Camera.cs`、`War3CameraComponent.cs:105-116` |
| 光照方向、颜色、环境光、昼夜曲线 | 生成表 `LightingTable`（源：`Environment\DNC`） |
| 雾颜色、起止距离 | `FogOfWarCommon.glsl` |
| 迷雾格宽、边缘柔化 | `Src/Core/FogOfWar.cs:13-14` |
| 地形贴图混合权重、阴影强度 | `TerrainMaterialBatches.cs`、`TerrainShadowMap.cs:35-36` |
| 天空盒日月与云 | `SkyboxRenderer.cs:14-61` |
| 水面参数 | `WaterRenderer.cs` |
| 队伍色、幻象染色 `(20,96,255)` | `UnitStateColor.cs:16-30` |
| 选择圈贴图与尺寸 | `Presentation.cs:1844` |
| 动画过渡时长、行走速率系数 | `UnitAnimationController.cs` |
| 血条、浮动文字字号与颜色 | `Floats.cs` |

### 5.3 坐标与单位

WC3 世界单位当 Godot 世界单位用（1:1）。轴向集中在 `Bridge/Coords.cs`：

```
worldX = -(mapX - terrain.OffsetX)
worldZ =  mapY - terrain.OffsetY
```

所有进出模拟的坐标都过这个文件。口径一乱，截图逐项对比永远对不上。

### 5.4 逐项对齐清单

| # | 项 | 客户端 | Godot 实现 | 阶段 |
|---|---|---|---|---|
| 1 | 地形网格与贴图混合 | `TerrainRenderer`、`Terrain.frag.glsl` | `View/Terrain` | P2 |
| 2 | 悬崖与断崖接缝 | `CliffRenderer.cs` | `View/Terrain/Cliff` | P2 |
| 3 | 地形烘焙阴影 | `TerrainShadowMap.cs:35-36` | 预烘焙贴图 | P2 |
| 4 | 装饰物实例与动画 | `DoodadRenderer.cs:229-294` | `View/Doodads` | P2 |
| 5 | 装饰物破坏与消失 | `DoodadRenderer.cs:958-978` | 动画 + 淡出 | P2 |
| 6 | 昼夜光照 | `SceneLighting.cs` + `LightingTable` | `WorldEnvironment` + 方向光 | P2 |
| 7 | 天空盒日月云 | `SkyboxRenderer.cs` | `View/Sky` | P2 |
| 8 | 水与岸边浪 | `WaterRenderer.cs`、`ShorelineWaveRenderer.cs` | `View/Water` | P2 |
| 9 | 战争迷雾（含 LOS 遮挡） | `FogOfWar.cs:566-597` | shader + 贴图 | P2 |
| 10 | 单位模型与骨骼动画 | `CharacterRenderer.cs` | `View/Units` + AnimationPlayer | P3 |
| 11 | 动画选择规则 | `UnitAnimationController.cs` | 决策表移植 | P3 |
| 12 | 附着点与武器挂点 | MDX 事件、`AttachmentPoints` | 从 tscn 的 `attach_*` 节点读 | P3 |
| 13 | 队伍色、幻象染色、虚无色调 | `UnitStateColor.cs` | 材质参数 | P3 |
| 14 | 选择圈、血条、施法条 | `Presentation.cs:1844` | `View/Hud/Overlay` | P3 |
| 15 | 技能特效与粒子 | `Effect.Visual*` | `View/Effects` | P3 |
| 16 | 弹道（弧形、直线、反弹） | `MissileEffect` 参数 | 同上 | P3 |
| 17 | 地面贴花、建筑残影 | `War3SplatSystem.cs`、`FogMemory.cs` | `View/Decals` | P3 |
| 18 | 天气 | `WeatherRenderer.cs`、`WeatherType.cs` | `View/Weather` | P3 |
| 19 | HUD 与命令卡 | `UI/` 18,045 行 | `View/Hud` | P4 |
| 20 | 加载界面 | `War3LoadingScreenRenderer.cs` | `View/Loading` | P4 |

### 5.5 Launcher 现有表现层怎么处理

| 现有内容 | 处理 |
|---|---|
| `Environment/`（地形、悬崖、水、天空、装饰物、天气、光照遮罩） | 保留，按清单补参数，接 `LightingTable` |
| `Presentation/UnitSceneComponent`、`EffectArtTable` | 保留骨架，数据源换成模拟契约；148 条硬编码改为读物编 `Art` |
| `Camera/War3Camera` | 保留，补齐旋转、跟随、平滑、边界 |
| `UI/`（HUD、小地图、英雄栏、聊天、菜单、加载） | 保留骨架，按 FDF 重排 |
| `Audio/` | 保留，声道策略按客户端重写 |
| `Scenes/War3GameScene` 的 `Ai`、`AiCounters`、`Melee`、`Production`、`Commands`、`Rally`、`QuickCast`、`Units`、`Lobby`、`Cheats` | 删除，6,531 行 |
| `Diagnostics/`（CombatProbe 等） | 删除，6,261 行，P6 换成客户端诊断 |
| `Src/` | 删除，18,475 行 |
| `GameCore/L1` `L2` `L3`（Launcher 副本） | 删除，17,957 行，由 `Client/` 取代 |

合计删除 49,224 行，剩约 10,000 行，P6 目标压到 9,000。

---

## 6. 阶段计划

| 阶段 | 目标 | 关键交付 | 主要风险 |
|---|---|---|---|
| P0 | 数据落地 | codegen、file source、数据哈希对账 | 生成与客户端不一致 |
| P1 | 逻辑同源 | 模拟层搬迁、w3x 直读、JASS、digest | 双引擎并存期 |
| P2 | 世界画面 | 地形、悬崖、水、天空、光照、装饰物、迷雾 | 参数对不齐 |
| P3 | 单位与特效 | 模型、动画规则、附着点、染色、特效、贴花、天气 | 动画决策移植 |
| P4 | 界面与交互 | FDF 界面、命令卡、输入、加载界面 | 布局口径 |
| P5 | 音频与 gmap | 音频端口、gmap 容器 | 混音模型 |
| P6 | 收尾 | 删旧实现、性能、打包 | 性能不及客户端 |

### P0 数据落地

| 任务 | 内容 |
|---|---|
| P0.1 | 建 `Client/` `View/` 三工程，接同步脚本 |
| P0.2 | net8 兼容：A1、A2 两处源码 + A5 工程标签 + 包引用 |
| P0.3 | `Extract/tools/DataCodegen`：net11 控制台，引用客户端工程，跑 `War3ObjectData.Load`，导出成品表为 C# |
| P0.4 | 生成 `MiscTexts` / `MetaTables` / `JassScripts` / `AiScripts` |
| P0.5 | `IWarcraftFileSource` 回退 + Launcher 实现（读生成常量） |
| P0.6 | `War3ObjectData` 加 `partial`，写 `LoadGenerated` |
| P0.7 | `--data-probe`：打印六组计数 + 数据哈希；`--asset-probe` 清点美术缺件 |
| P0.8 | 资源整理：`wc3.json` 不进 pck（是否停发看 Wiki），`assets/common` 随光照转 C# 后删除 |
| P0.9 | 单程序集合并：内联 Client 与 War3Net、删自有模拟层、L4 移出编译（§3.5 第 1、2 步） |
| P0.10 | 把 `--data-probe` 与 `--data-codegen` 收进 Launcher，删除 DataProbe / DataCodegen 两个工具工程与 `Extract/tools`（§3.5 第 3 步） |

验收：

- `--data-probe` 的 `unit/ability/buff/item/destructable/upgrade` 六组计数与客户端 Debug 构建打印的 `全量物编: …`（`War3ObjectData.cs:79-81`）逐项相等
- `--data-probe` 算出的哈希与 `data-codegen` 写进 `DataDigest.g.cs` 的值相等
- `--asset-probe` 缺件为零
- 生成源码 ≤ 6 MB，增量编译 ≤ 30 s

进展（2026-11-13）：P0.1 至 P0.7 已完成。Client 995 个文件在 net8 编译通过，生成表 5.4 MB、文本源 1.5 MB，`--data-probe` 与客户端 `War3ObjectData.Load` 的规范化哈希在 5 张对战图上完全一致。

回退：codegen 是增量工具，不改变 Extract 现有产物。

### P1 逻辑同源

| 任务 | 内容 |
|---|---|
| P1.1 | 搬 `L1` `L2` `L3` `Src`；删 Launcher 的 `Src/` 与 `GameCore/L1` `L2` `L3`（36,432 行） |
| P1.2 | 地图：w3x 直读，走客户端 `War3Map*` 解析链 |
| P1.3 | `GameSession` 驱动：20Hz 固定步，`World.Step` + `JassRuntimeHost.Step` |
| P1.4 | `Bridge/CommandBridge` 最小集：选择、移动、攻击、停止、建造、训练 |
| P1.5 | 视图降级：地形 + 单位方块 |
| P1.6 | `--state-digest`：逐 tick 规范化摘要 |

digest 字段：

| 分组 | 字段 |
|---|---|
| 单位 | id、类型码、玩家、位置（定点到 1e-3）、朝向、HP、MP、当前订单 id、目标 |
| 玩家 | 金、木、人口、人口上限、科技等级表 |
| 世界 | tick、随机流状态、单位数、尸体数、特效数 |
| 脚本 | JASS 全局变量哈希、触发器队列长度 |

验收：

- 5 张对战图 × 3000 tick，与客户端 `--headless --digest` 逐 tick 一致
- 同一输入脚本下命令日志逐条一致
- `--engine=launcher` 开关保留，旧引擎仍可跑

### P2 世界画面

| 任务 | 内容 |
|---|---|
| P2.1 | `Bridge/Coords` 落地，清掉视图里的手算坐标 |
| P2.2 | `View/Terrain`：网格、图层混合、悬崖、烘焙阴影 |
| P2.3 | `View/Doodads`：实例化、动画、破坏 |
| P2.4 | `View/Lighting`：读 `LightingTable`，按时间插值驱动 `WorldEnvironment` 与方向光 |
| P2.5 | 天空、水、岸边浪 |
| P2.6 | 迷雾：读模拟可见性数据，shader 出图 |
| P2.7 | 相机：距离、俯角、滚轮、边缘滚屏、跟随 |

验收：清单 1 至 9 项逐项截图为绿；8 组机位（俯视、贴地、水边、夜间、迷雾内、悬崖边、密林、大战场）SSIM ≥ 0.99。

### P3 单位与特效

| 任务 | 内容 |
|---|---|
| P3.1 | `Bridge/UnitView` 快照 + 实例池；单位增删走事件 |
| P3.2 | 模型实例化：`res://extract/**/*.tscn`；序列取 `*.anim.tres` 的 `AnimationLibrary`，附着点取 `BoneAttachment3D` |
| P3.3 | 动画规则移植：状态 → 序列标签、过渡时长、行走速率。库键就是 MDX 原名，客户端决策表可直接映射 |
| P3.4 | 附着点、武器挂点、缩放 |
| P3.5 | 队伍色、幻象染色、虚无色调、隐身透明度 |
| P3.6 | 选择圈、血条、施法条、浮动文字 |
| P3.7 | 特效：按 `Effect.Visual*` 实例化 |
| P3.8 | 弹道：按 `MissileEffect` 的进度与弧度插值 |
| P3.9 | 贴花、建筑残影、天气 |

验收：清单 10 至 18 项为绿；抽 20 个单位逐帧比对动画序列名与播放速率；一次 3v3 交战录像逐帧看一遍，无穿模、无残留、无闪烁。

### P4 界面与交互

| 任务 | 内容 |
|---|---|
| P4.1 | FDF → Godot Control：锚点、层级、九宫格、字体（源为 `FdfSources.g.cs`） |
| P4.2 | 命令卡：用客户端算好的格位，不再顺序填 |
| P4.3 | 队列格（可点取消）、英雄栏、小地图、聊天、任务/排行榜/多面板 |
| P4.4 | 输入全套：选择、编队（Shift 并入）、右键智能指令、热键、滚轮、边缘滚屏、Esc/F10 |
| P4.5 | 加载界面：阶段文本、进度条、地图预览、玩家列表 |

验收：清单 19、20 项为绿；输入回放 200 条命令日志一致；逐屏截图（加载、主 HUD、命令卡、建造子页、技能学习、任务、结算）。

### P5 音频与 gmap

| 任务 | 内容 |
|---|---|
| P5.1 | 音频：24 声道、8 组配额、优先级抢占、循环音 |
| P5.2 | 3D 衰减在 C# 侧按 OpenAL 公式算成 `volume_db` |
| P5.3 | 音乐双通道与间隔 |
| P5.4 | gmap 容器：w3x 段原样装进 gmap，读出来仍是同一批字节 |

验收：音频事件日志与客户端一致；5 个距离点音量差 ≤ 0.5 dB。

### P6 收尾

| 任务 | 内容 |
|---|---|
| P6.1 | 删除旧引擎与 `--engine=launcher` |
| P6.2 | 旧 `--state-probe` 退役，改用客户端 `Diagnostics/` 中不依赖窗口的诊断 |
| P6.3 | 性能：draw call、CPU 帧时间、内存、加载耗时 |
| P6.4 | 打包 macOS / Windows |

验收：1v1 中后期（80 单位 + 60 建筑 + 特效）帧时间 ≤ 客户端 × 1.1。

---

## 7. 验证体系

### 7.1 数据：哈希对账

```bash
# 生成期
dotnet run --project Extract/tools/DataCodegen -- --war3=<安装目录> --out=Launcher/Client/Data/Generated
# 运行期
Godot --path Launcher --headless -- --data-probe
```

两边哈希相等即通过。这套哈希覆盖每个对象的全部字段与分级字段，任何一处合并顺序错、变体回填漏、`authoritative` 语义反了都会立刻暴露。

### 7.2 逻辑：逐 tick 摘要

```bash
OpenWC3.Headless <map.w3x> --war3=<dir> --ticks=3000 --digest
Godot --path Launcher --headless -- --play=<map.w3x> --ticks=3000 --digest
```

输出 `tick<TAB>digest`，`diff` 必须为空。首个不一致的 tick 打印逐单位差异。

### 7.3 画面：机位快照

```bash
tools/shot-compare.sh <map.w3x> <镜头预设> <tick>
# → out/launcher.png、out/client.png、out/diff.png、out/metrics.txt
```

判定三条同时满足：SSIM 与均值差达线、无整块缺失、差异集中在边缘与高频区。批量 8 机位 × 3 时间点。

### 7.4 界面：逐屏

加载、主 HUD、命令卡、建造子页、技能学习、商店、任务、排行榜、结算、Esc 菜单，每屏一张对比图。

### 7.5 交互：输入回放

```
0    select 0 12 34
0    move 1500 2400
40   train hfoo
120  cast AHea 3
```

两侧执行同一脚本，比对命令日志与最终 digest。

### 7.6 美术：缺件检查

`--asset-probe` 遍历当前地图实际用到的模型、贴图、音频、界面图，逐个走 `Bridge/AssetPath` 解析，输出缺失清单。缺件必须是零。

---

## 8. 风险与预案

| 风险 | 影响 | 触发条件 | 预案 |
|---|---|---|---|
| 生成数据与客户端不同步 | 高 | 客户端改了加载逻辑，生成器没跟 | P0 的哈希门禁；每次同步后重跑 codegen |
| 生成源码过大拖慢编译 | 中 | 超 6 MB 或编译超 30 s | 按表拆文件 → 按首字母分片 → 紧凑编码 |
| 门面语义错位 | 高 | 视图读到的字段含义不一致 | `UnitView` 单文件对齐；`--view-probe` 抽 50 个单位逐字段比对 |
| 表现层双实现导致漂移 | 高 | 客户端改了画面参数 | §5.2 参数表集中登记；同步后跑截图回归 |
| 美术缺件 | 中 | 某类资源没进 pck | `--asset-probe` 零缺件才进 P2 |
| 动画规则移植不全 | 中 | 采集、建造、引导施法序列不对 | 按 `UnitAnimationController` 决策表逐状态列对照 |
| 坐标口径被破坏 | 中 | 某处手算坐标 | `Coords` 唯一入口 |
| 地图自定义导入资源 | 中 | 带自定义模型/贴图的地图 | 先支持对战图，导入资源由 Map 工程在 gmap 转换期处理 |
| net8 兼容被破坏 | 中 | 客户端用了 net9+ API | 同步脚本带 net8 编译检查 |
| 性能不及客户端 | 中 | 节点数、draw call 高 | 实例池、MultiMesh、可见性剔除；用 `DrawCallCount` 对照 |

---

## 9. 迭代规程

### 9.1 每轮

```
1. 选切片：从当前阶段任务表取一个能被 gate 验证的最小单元，不跨阶段。
2. 写预期：动哪些文件、哪条 gate 从红转绿、指标怎么变。
3. 实现：模拟层保持原样，改动落在 View / Bridge / 生成器。
4. 跑 gate：构建（0 错 0 警告）→ 数据哈希（P0 起）→ digest（P1 起）→ 截图（P2 起）。
5. 记账与提交：<阶段><序号> <一句话结论>，正文写改了什么、为什么、gate 前后数值。
6. 不达标就回退：文件、工程引用、生成表、登记表全部还原，git status 干净。
```

### 9.2 停止判据

连续两轮 gate 无推进，停下来改计划，写清：卡在哪条 gate、已排除哪些原因、建议改哪一段。

### 9.3 不许做的事

| 禁令 | 原因 |
|---|---|
| 为了过 gate 改 gate | 判据一松，后面所有结论都不可信 |
| 手工编辑 `Client/Data/Generated/` | 下次 codegen 会被覆盖，改生成器 |
| 在 `Client/` 里写 `using Godot` | 模拟层必须能无头运行 |
| 在 `View/` 里直接读 `Client` 的 `Unit` | 破坏分层，语义漂移没人管得住 |
| 视图里出现裸数字 | 参数必须进 `ViewTuning` 并注明出处 |
| 用 `diff -rq` 判差异 | 同路径文件行重合度低，只会说「都不一样」 |

---

## 10. 指标表

| 阶段 | 同源文件数 | 数据哈希 | digest 一致 tick | 截图 SSIM | 清单关闭 |
|---|---:|---|---:|---:|---:|
| 起点 | 112 / 262 同路径 | — | — | — | 0 / 20 |
| P0 数据通路 | Client/ 995 个文件搬入 | 5 张对战图一致（53d620…） | — | — | 0 / 20 |
| | | | | | |

辅助指标：

| 指标 | 起点 | 目标 |
|---|---:|---:|
| Launcher 自有代码行数 | 59,211 | ≤ 9,000 |
| 客户端源码补丁处数 | — | ≤ 4 |
| 生成表源码体积 | 0 | ≤ 6 MB |
| 画面参数表条目 | 0 | ≥ 40，全部带出处 |
| 构建时间 | — | ≤ 90s |

---

## 附录 A：客户端源码补丁清单

| # | 文件 | 改动 | 原因 | 影响 |
|---|---|---|---|---|
| A1 | `L3_Simulation/…/Jass/JassGameNatives.Models.cs:222` | `dict.Capacity` → `dict.EnsureCapacity(n)` | `Dictionary.Capacity` 是 net9 API | 无 |
| A2 | `L1_Adapter/Formats/Jass/JassAotCompiler.cs:126` | net8 下条件编译掉 `PersistedAssemblyBuilder` 分支 | net9 API，AOT 属可选优化 | 加载期不预编译，退回解释执行 |
| A3 | `L3_Simulation/…/Configuration/War3ObjectData.cs:13` | `sealed class` → `sealed partial class` | 让 Launcher 补 `LoadGenerated` | 无 |
| A4 | `Src/Resources/War3ResourceManager.cs` | 加 `IWarcraftFileSource` 回退（约 15 行） | 让生成常量充当游戏档案 | 无 |
| A5 | `L0_Foundation/ThirdParty/War3Net/*.csproj` | TFM net9.0 → net8.0；`War3Net.IO.Mpq` 补 `War3Net.Common` 直接引用 | 工程标签 | 无 |
| A6 | `Src/Diagnostics/FdfParserTest.cs` | 不搬 | 引用界面类型 | 无 |

同步脚本每次同步后跑一遍 net8 编译，报错即定位。

## 附录 B：模块对照

| 客户端 | Launcher | 处理 |
|---|---|---|
| `Src/**`、`L1_Adapter/Formats`、`L2_Core`、`L3_Simulation` | `Client/` | 整体搬入 |
| 表格与文本配置 | `Client/Data/Generated/` | codegen 产物 |
| `L4/Game/Rendering`、`Environment` | `View/` | Godot 重写 |
| `L4/Game/UI` | `View/Hud` | Godot 重写 |
| `L4/Game/Scenes` | `View/Scene` | 只保留视图部分 |
| `L4/Game/Input` | `Bridge/Input` | 逻辑搬，设备换 Godot |
| `L4/Game/Audio` | `View/Audio` | 决策搬，播放换 Godot |
| `L4/Game/Presentation/UnitAnimationController` | `View/Units/AnimationRules` | 决策表移植 |
| `L4_Composition/Headless` | `--headless` | 参考其结构 |
| Launcher 现有 `Environment/`、`Presentation/`、`Camera/`、`UI/` | `View/` | 保留骨架，补参数 |
| Launcher 现有 `Src/` 与 `GameCore/L1` `L2` `L3` | 删除 | 由 Client 取代 |

## 附录 C：复现命令

```bash
# 1. 当前同源度
python3 - <<'EOF'
import os
L='Launcher'; C='test/Open-Warcraft3'
same=tot=0
for dp,_,fs in os.walk(L):
    if '/.godot' in dp or '/.git' in dp: continue
    for f in fs:
        if not f.endswith('.cs'): continue
        p=os.path.join(dp,f); rel=os.path.relpath(p,L); c=os.path.join(C,rel)
        if not os.path.exists(c): continue
        tot+=1
        same += open(p,'rb').read()==open(c,'rb').read()
print('同路径',tot,'字节一致',same)
EOF

# 2. net8 兼容检查（tools/net8-probe/probe.csproj）
#    net8.0 / LangVersion=preview / ImplicitUsings=enable / Nullable=disable
dotnet build Launcher/tools/net8-probe/probe.csproj -v q --nologo 2>&1 | grep -c "error"

# 3. 生成数据
dotnet run --project Extract/tools/DataCodegen -- --war3=<安装目录> --out=Launcher/Client/Data/Generated

# 4. 两侧截图
./side_by_side.sh <gmap> <w3x> 12 /tmp/sbs
```

## 附录 D：可复用资产

| 资产 | 用途 |
|---|---|
| `Extract/src/Core/Tools/War3Extract.cs` | MPQ 读取与遍历，codegen 直接复用 |
| `Extract/src/Core/Tools/LightingExportCli.cs` | DNC 解析，扩一个 C# 输出即可 |
| `--combat-probe`（攻击矩阵 89.3 / 115.4 / 150.0） | P1 数值回归 |
| `--state-probe` 764 条断言 | P1 双引擎对照，P6 退役 |
| 失效模式清单（`Launcher一致化任务.md` 第 6 节） | 调试排查表 |
| 客户端 `Diagnostics/` 104 个诊断入口 | P6 之后替代 Launcher 自有探针 |
