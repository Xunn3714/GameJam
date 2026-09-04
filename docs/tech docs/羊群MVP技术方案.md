# 《找到另一只羊》MVP 技术方案

- **状态**：Draft
- **Owner**：TBD
- **最后更新**：2026-09-04
- **目标版本**：Unity 6000.5.9f1
- **表现形式**：URP 2D 俯视角
- **首轮平台**：Windows，WASD 键盘操作
- **玩法场景**：`Assets/_Game/Scenes/Level_01.unity`
- **计划实现路径**：`Assets/_Game/`
- **上游策划**：[羊群 MVP 策划案](../design/羊群MVP策划案.md)
- **Agent 入口**：[`AGENTS.md`](../../AGENTS.md)

## 1. 目标

实现一个约 5 分钟的最小可玩版本，验证以下核心体验：

> 玩家从一只羊开始，通过接触另外 5 只羊，让族群逐渐扩大。

第一版同时加入“拉屎”主动技能，用来验证技能的输入、生成物、反馈和冷却流程。

每只羊在单局开始时获得一个随机且不重复的名称。游戏完成时显示简单的本局统计，用来验证运行时数据采集和结算展示流程。

## 2. 当前工程基础

仓库目前已经提供 Unity 2D 基线和基础游戏流程：

- Unity `6000.5.9f1`。
- Universal Render Pipeline / 2D Renderer。
- Unity Input System `1.20.0`。
- uGUI `2.5.0`。
- `Assets/InputSystem_Actions.inputactions` 已包含 `Player/Move`，支持 WASD、方向键和手柄。
- `MainMenu → Level_01` 已接入 Build Settings。
- `Level_01` 已包含 Camera、Global Light 2D、GameCanvas、EventSystem 和 PauseManager。
- 已有持久化的 `SceneLoader` 和 `AudioManager`，由 MainMenu 创建并跨场景保留。
- 已有暂停、设置、主菜单和 Ending 基础系统。
- 尚无玩家移动、羊群、任务、技能、名称和结算统计模块。

本竖切片复用现有 `SceneLoader`、`AudioManager` 和 `PauseManager`，不新增全局 Manager、Event Bus、Service Locator、对象池或存档系统。

## 3. 玩家流程

1. 进入关卡，玩家控制一只带随机名称的羊。
2. HUD 显示 `找到羊：0/5` 和 `族群：1`。
3. 玩家使用 WASD 在草地中移动。
4. 玩家接触一只带随机名称的待招募羊，对方加入并开始跟随。
5. HUD 更新任务进度和族群数量，并显示 `“{羊名}”加入了族群！`。
6. 玩家可以按 Space 拉屎；技能进入短暂冷却。
7. 找到第 5 只羊后显示完成面板，列出成员名称、族群规模、拉屎次数和游戏用时。
8. 玩家可以重新开始本关。

第一版没有失败条件。

## 4. 文件结构

```text
Assets/_Game/
├── Runtime/
│   ├── Player/
│   │   └── SheepPlayerController.cs
│   ├── Flock/
│   │   ├── FlockController.cs
│   │   ├── RecruitableSheep.cs
│   │   ├── SheepFollower.cs
│   │   └── SheepIdentity.cs
│   ├── Naming/
│   │   ├── SheepNameGenerator.cs
│   │   └── SheepNamePool.cs
│   ├── Skills/
│   │   └── PoopAbility.cs
│   ├── Stats/
│   │   └── MvpSessionStats.cs
│   ├── Tasks/
│   │   └── RecruitSheepTask.cs
│   ├── UI/
│   │   ├── MvpHudView.cs
│   │   ├── JoinToastView.cs
│   │   └── ResultPanelView.cs
│   ├── GameFlow/
│   │   └── MvpGameController.cs
│   ├── Camera/
│   │   └── CameraFollow2D.cs
├── Content/
│   ├── Art/Characters/Sheep/
│   ├── Data/
│   │   └── SheepNamePool.asset
│   └── Perfabs/Sheep/
│       ├── PlayerSheep.prefab
│       ├── RecruitableSheep.prefab
│       └── Poop.prefab
└── Scenes/
    └── Dev/
        └── SheepMvp_Dev.unity
```

当前主分支没有 asmdef，现有 `SceneLoader`、`AudioManager` 和 `PauseManager` 都属于 `Assembly-CSharp`。本 MVP 暂不新增 asmdef，以免新程序集无法引用这些现有类型。

当前仓库实际目录名是 `Perfabs`。本文暂时沿用该路径，避免同时出现 `Prefabs` 和 `Perfabs` 两套目录；后续如需纠正拼写，必须由目录 owner 在 Unity Project 窗口内统一移动并保留 GUID。

现有 `PixelArtImporter` 只匹配路径中包含 `Art/Sprites` 的图片，而当前羊美术计划放在 `Art/Characters/Sheep`。占位图需要在 Inspector 中手动设置 Point Filter、无压缩、无 Mipmap 和16 PPU；是否扩展导入器由美术管线 owner 另行决定。

首轮功能开发在独立 Dev Scene 中完成。通过验收后，由 `Level_01` Scene owner 将 Prefab 和 HUD 集成到现有 `Level_01`，不修改 `SampleScene`。

## 5. 与现有系统的集成

- 重新开始优先调用 `SceneLoader.Instance.ReloadCurrentScene()`。
- 加入羊和拉屎音效调用 `AudioManager.Instance.PlaySFX(clip)`；直接运行 Dev Scene、实例不存在时静默跳过音效。
- `Level_01` 的 PauseManager 会设置 `Time.timeScale = 0`。玩家移动和拉屎技能必须在暂停时停止响应。
- HUD、加入提示和结算面板集成到现有 `GameCanvas` 下，不创建第二个 Canvas 或 EventSystem。
- MVP 完成时在 `Level_01` 内显示结算面板，暂不连接 Ending Scene。

## 6. 核心组件

### 6.1 SheepPlayerController

负责玩家移动：

- 通过 Inspector 引用现有 `Player/Move` Input Action。
- `Update` 读取 `Vector2` 输入。
- 输入向量归一化，避免斜向加速。
- `FixedUpdate` 使用 `Rigidbody2D.MovePosition` 移动。
- 记录最后移动方向，供 Sprite 朝向和羊群队形使用。
- `Time.timeScale == 0` 时不处理移动。
- 游戏完成时允许关闭输入。

初始参数：

| 参数 | 默认值 |
|---|---:|
| 移动速度 | 4 |
| Rigidbody2D 类型 | Dynamic |
| Gravity Scale | 0 |
| Freeze Rotation Z | 开启 |

### 6.2 RecruitableSheep

挂载在场景里的 5 只待招募羊上。

```csharp
public bool TryRecruit(FlockController flock);
```

- 首次接触返回 `true` 并加入族群。
- 已加入后再次接触返回 `false`。
- 加入后关闭待机逻辑并启用 `SheepFollower`。
- 第一版待招募羊停留在原地，只播放待机动画。
- 加入提示使用 `SheepIdentity.DisplayName`，例如 `“棉花糖”加入了族群！`。

招募判断通过组件引用完成，不依赖 Tag、对象名字或全局查找。

### 6.3 FlockController

负责登记羊和分配跟随位置。

```csharp
public int RecruitedCount { get; }
public int TotalCount => RecruitedCount + 1;

public event Action<RecruitableSheep, int> SheepRecruited;
public bool Add(RecruitableSheep sheep);
```

- `RecruitedCount`：新找到的羊，范围为 0～5。
- `TotalCount`：包含玩家羊，范围为 1～6。
- 成功加入后只发出一次 `SheepRecruited` 事件。

玩家 Prefab 下设置 5 个可在 Inspector 调整的跟随点：

```text
FormationRoot
├── Slot_01  (-0.7, -0.8)
├── Slot_02  ( 0.7, -0.8)
├── Slot_03  ( 0.0, -1.5)
├── Slot_04  (-0.8, -2.2)
└── Slot_05  ( 0.8, -2.2)
```

`FormationRoot` 保持玩家最后一次移动方向。第一版不实现 Boids 或 NavMesh。

### 6.4 SheepFollower

- 接收一个跟随点 `Transform`。
- 使用 `Vector2.MoveTowards` 向目标移动。
- 默认速度为 `4.5`。
- 与目标距离小于 `0.1` 时停止。
- 距离大于 `3` 时临时提升到 `6`，减少掉队。
- 跟随羊不参与实体碰撞，避免互相推挤和抖动。

第一版地图保持开阔，允许跟随羊轻微穿过装饰物。

### 6.5 SheepIdentity

每只玩家羊和待招募羊都挂载 `SheepIdentity`：

```csharp
public string DisplayName { get; private set; }
public void AssignName(string displayName);
```

- 名称只在本局运行时写入，不保存到 Prefab 或 ScriptableObject。
- 名称用于加入提示和最终成员名单。
- 第一版不在羊头顶持续显示名称，避免增加世界空间 UI。

### 6.6 SheepNamePool 与 SheepNameGenerator

`SheepNamePool` 是只读 ScriptableObject 配置，策划可以在 Inspector 中编辑候选名称。建议初始提供至少 12 个名称：

```text
棉花糖、云朵、小卷、豆豆、奶盖、白团、咩咩、毛球、盐巴、小雪、软糖、月亮
```

`SheepNameGenerator` 在单局开始时为玩家羊和 5 只待招募羊分配名称：

```csharp
public string NextUniqueName();
```

规则：

- 忽略空字符串和纯空格名称。
- 单局内优先保证名称不重复。
- 如果有效名称数量不足，使用 `小羊 01`、`小羊 02` 等后备名称。
- 重新开始关卡后重新随机。
- 第一版不要求固定随机种子，也不跨局保存名称。

### 6.7 RecruitSheepTask

纯 C# 任务对象，目标固定为 5，方便编写 EditMode 测试。

```csharp
public int Target { get; }
public int Progress { get; private set; }
public bool IsComplete => Progress >= Target;

public event Action<int, int> ProgressChanged;
public event Action Completed;
```

任务只接收成功招募结果，不直接处理碰撞或 UI。

### 6.8 PoopAbility

在 `Assets/InputSystem_Actions.inputactions` 的 `Player` Action Map 中新增：

```text
Action: Poop
Type: Button
Keyboard: Space
```

不复用模板中的 `Attack`，避免语义混乱。

释放流程：

```text
按下 Space
→ 检查是否冷却完成
→ 在 PoopSpawnPoint 生成 Poop.prefab
→ 播放简单动画或音效
→ HUD 显示剩余冷却
→ 成功生成后统计拉屎次数加一
→ 冷却结束后恢复可用
```

初始参数：

| 参数 | 默认值 |
|---|---:|
| 冷却时间 | 2 秒 |
| 粪便存在时间 | 10 秒 |
| 场上最大数量 | 10 个 |
| 开局状态 | 可立即使用 |

只有成功生成粪便时才增加统计次数；冷却期间的无效按键不计数。暂停和游戏完成后不能释放。

第一版粪便没有伤害或生态效果，只验证主动技能链路。后续可以扩展施肥、减速、吸引生物或标记道路。

### 6.9 MvpSessionStats

`MvpSessionStats` 是普通运行时 C# 对象，不是 ScriptableObject，也不写入 PlayerPrefs。

```csharp
public int RecruitedCount { get; private set; }
public int TotalFlockCount => RecruitedCount + 1;
public int SuccessfulPoopCount { get; private set; }
public float ElapsedSeconds { get; private set; }
public IReadOnlyList<string> MemberNames { get; }

public void RecordRecruit(string sheepName);
public void RecordPoop();
public void Tick(float deltaTime);
```

统计规则：

- 创建本局时先记录玩家羊名称。
- 每次成功招募记录数量和新成员名称。
- 每次成功生成 `Poop.prefab` 后记录一次拉屎。
- 计时使用 `Time.deltaTime`，暂停时间不会计入。
- 游戏完成后停止计时。
- 重新加载关卡后全部重置，不跨局保存。

第一版结算字段保持简单：

```text
族群规模：6
拉屎次数：12
游戏用时：01:43
成员：云朵、豆豆、奶盖、白团、毛球、小雪
```

### 6.10 MvpGameController

负责连接羊群、任务和 UI：

- 初始化目标为 5 的 `RecruitSheepTask`。
- 使用 `SheepNameGenerator` 为 6 只羊分配单局名称。
- 创建并持有本局 `MvpSessionStats`。
- 监听 `FlockController.SheepRecruited`。
- 驱动任务进度、名称提示、统计和 HUD。
- 监听 PoopAbility 的成功释放事件并记录次数。
- 第 5 只羊加入后只触发一次完成状态。
- 完成时停止统计计时、关闭玩家输入并显示结算面板。
- 重新开始优先调用现有 `SceneLoader.Instance.ReloadCurrentScene()`。

### 6.11 UI

HUD 使用 uGUI，Canvas 参数：

- Render Mode：Screen Space - Overlay。
- Canvas Scaler：Scale With Screen Size。
- Reference Resolution：1920×1080。
- Match：0.5。

界面内容：

| 状态 | 任务 | 族群 | 技能 |
|---|---|---|---|
| 开局 | 找到羊：0/5 | 族群：1 | Space：拉屎 |
| 加入后 | 找到羊：1/5 | 族群：2 | Space：拉屎 |
| 冷却中 | 保持当前进度 | 保持当前数量 | 拉屎：1.4秒 |
| 完成 | 找到羊：5/5 | 族群：6 | 打开本局统计 |

`JoinToastView` 每次招募时显示 `“{羊名}”加入了族群！`，持续 1.5 秒后淡出。

`ResultPanelView` 在完成时显示：

- `你找到了所有羊！`
- 族群规模。
- 拉屎次数。
- 格式化后的游戏用时。
- 本局成员名称列表。
- 重新开始按钮。

## 7. 场景结构

```text
Level_01
├── Main Camera
├── Global Light 2D
├── SheepGameplay
│   ├── MvpGameController
│   ├── PlayerSheep
│   ├── Sheep_01
│   ├── Sheep_02
│   ├── Sheep_03
│   ├── Sheep_04
│   └── Sheep_05
├── GameCanvas（已有）
│   ├── SheepHUD
│   │   ├── TaskText
│   │   ├── FlockCountText
│   │   ├── PoopCooldownText
│   │   └── JoinToast
│   ├── ResultPanel
│   │   ├── ResultText
│   │   ├── MemberNamesText
│   │   └── RestartButton
│   └── PausePanel（已有）
├── PauseManager（已有）
└── EventSystem（已有）
```

建议地图约为 `20 × 14` 个世界单位。

初始位置参考：

| 对象 | 坐标 |
|---|---|
| PlayerSheep | `(0, 0)` |
| Sheep_01 | `(-4, 2)` |
| Sheep_02 | `(4, 3)` |
| Sheep_03 | `(-6, -3)` |
| Sheep_04 | `(6, -2)` |
| Sheep_05 | `(1, 6)` |

## 8. 数据流

```text
玩家接触羊
→ RecruitableSheep.TryRecruit()
→ FlockController.Add()
→ SheepRecruited 事件
→ MvpGameController
→ RecruitSheepTask 更新进度
→ MvpSessionStats 记录羊名和数量
→ HUD 显示带名称的加入提示
→ 达到 5/5 后进入完成状态
→ ResultPanelView 显示本局统计
```

```text
玩家按 Space
→ PoopAbility 检查冷却和场上数量
→ 生成 Poop.prefab
→ MvpSessionStats 记录成功释放
→ 更新技能 UI
→ 10 秒后销毁粪便
```

## 9. 配置

| 配置 | 编辑者 | 读取者 | 默认值 | 校验规则 | 运行时可变 |
|---|---|---|---|---|---|
| 任务目标 | 策划 | RecruitSheepTask | 5 | 大于0且不超过场景羊数 | 否 |
| 名称池 | 策划/文案 | SheepNameGenerator | 至少12个名称 | 去空、去重，不足时使用后备名 | 否 |
| 玩家速度 | 策划/程序 | SheepPlayerController | 4 | 大于0 | 否 |
| 跟随速度 | 策划/程序 | SheepFollower | 4.5 | 大于0 | 追赶时临时变化 |
| 拉屎冷却 | 策划 | PoopAbility | 2秒 | 大于等于0 | 否 |
| 粪便寿命 | 策划 | PoopAbility | 10秒 | 大于0 | 否 |
| 粪便上限 | 策划 | PoopAbility | 10 | 大于0 | 否 |

静态名称池使用 ScriptableObject；本局名称、任务进度和统计数据使用普通运行时对象。

## 10. ProjectSettings 约束

`ProjectSettings/` 当前没有被 `.gitignore` 忽略，并且属于仓库已跟踪的 Unity 项目配置。它不应被整体忽略。

本 MVP 的约束是：**不修改任何 ProjectSettings 文件**。

因此第一版：

- 不新增 Tag。
- 不新增 Layer。
- 不新增 Sorting Layer。
- 不修改 Physics 2D 碰撞矩阵。
- 不修改 Build Settings。
- 不修改分辨率或目标平台。

招募使用组件检测；显示层级暂时使用 `SpriteRenderer.sortingOrder`；Dev Scene 直接在 Editor 中运行，不加入 Build Settings。

## 11. 实现顺序

1. 创建 `SheepMvp_Dev`，实现 WASD 移动和自定义相机跟随。
2. 制作玩家羊和待招募羊 Prefab。
3. 实现随机名称分配和单局去重。
4. 实现接触招募和 5 个固定跟随点。
5. 实现任务计数、带名字的加入提示和 HUD。
6. 实现 Space 拉屎、生成物限制和冷却 UI。
7. 实现 `MvpSessionStats` 与结算面板。
8. 接入现有 SceneLoader、AudioManager 和暂停状态。
9. 添加 EditMode 测试。
10. 由 Level_01 owner 集成并在 Unity 中完成完整试玩和编译验证。

## 12. 验收标准

- WASD 可以稳定控制玩家羊。
- 斜向移动不会更快。
- 每只羊只能加入一次。
- 玩家羊和 5 只待招募羊都有非空名称。
- 单局 6 个羊名不重复；名称池不足时正确使用后备名称。
- 加入提示显示实际加入羊的名称。
- 任务进度严格从 `0/5` 增加到 `5/5`。
- 族群数量严格从 1 增加到 6。
- 5 只羊都能持续跟随玩家。
- 加入提示每次只出现一次。
- Space 能生成粪便。
- 冷却期间不能重复释放。
- 场上粪便不超过 10 个，并在 10 秒后销毁。
- 拉屎次数只统计成功生成的粪便。
- 游戏用时不包含暂停时间，并在完成后停止增加。
- 结算正确显示族群规模、拉屎次数、游戏用时和成员名单。
- 第 5 只羊加入后只触发一次完成界面。
- 重新开始后羊、任务、UI 和技能状态全部重置。
- Unity Console 无编译错误或 Missing Reference。
- 本功能没有修改 `ProjectSettings/`。

## 13. 第一版不实现

- 冲撞和其他群体技能。
- 牛、猴子或其他物种。
- 生老病死、繁殖和疾病。
- 食物、饥饿和随机事件。
- 多关卡和人类结局。
- 名称存档、名称本地化和玩家自定义命名。
- 跨局数据保存、排行榜和长期统计。
- 通用技能框架、全局事件系统或复杂羊群算法。
