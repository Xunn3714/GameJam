# 《找到另一只羊》MVP 技术方案

- **状态**：历史 MVP 原型（当前主玩法已转为 `AlphaFlockExpansion`）
- **Owner**：TBD
- **最后更新**：2026-09-04
- **目标版本**：Unity 6000.5.9f1
- **表现形式**：URP 2D 俯视角
- **首轮平台**：Windows，WASD 键盘操作
- **历史场景**：`Assets/_Game/Scenes/Old/Legacy/Level_01.unity`
- **当前主场景**：`Assets/_Game/Scenes/AlphaFlockExpansion.unity`
- **实现路径**：`Assets/_Game/`、`Assets/Art/SheepSprites/`
- **上游策划**：[羊群 MVP 策划案](../design/羊群MVP策划案.md)
- **Agent 入口**：[`AGENTS.md`](../../AGENTS.md)

## 1. 目标

实现一个约 5 分钟的最小可玩版本，验证以下核心体验：

> 玩家从一只羊开始，通过接触本局随机生成的羊，让族群逐渐扩大。

第一版同时加入“拉屎”主动技能，用来验证技能的输入、存量恢复、生成物和反馈流程。

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
- 羊群移动、招募、名称、技能、任务、图鉴占位和结算统计已经集成到 `Level_01`。
- `MvpGameController` 会按 Inspector 参数创建本局羊群，不依赖 Scene 中预摆的待招募羊数量。
- 当前场景配置为 `100 × 50` 生成区域、50 只待招募羊；每个生成位独立进行品质判定。

本竖切片复用现有 `SceneLoader`、`AudioManager` 和 `PauseManager`，不新增全局 Manager、Event Bus、Service Locator、对象池或存档系统。

## 3. 玩家流程

1. 进入关卡，玩家控制一个包含 1 只随机命名成员羊的族群。
2. HUD 显示本局任务列表和 `族群：1`。
3. 玩家使用 WASD 在草地中移动。
4. 任意族群成员接触一只带随机名称的待招募羊，对方加入族群。
5. HUD 更新任务进度和族群数量，并显示 `“{羊名}”加入了族群！`。
6. 玩家可以按 Space 拉屎；技能消耗存量并按时间逐格恢复。
7. 完成全部必做任务后显示完成面板，列出成员名称、族群规模、招募数、分数、拉屎次数和游戏用时。
8. 玩家可以打开暂停菜单中的同伴名册/羊图鉴，或在结算后返回标题。

第一版没有失败条件。

## 4. 文件结构

```text
Assets/_Game/
├── Runtime/
│   ├── Flock/
│   │   ├── FlockController.cs
│   │   ├── FlockMovementController.cs
│   │   ├── FlockSizeObstacle.cs
│   │   ├── RecruitableSheep.cs
│   │   ├── SheepFlockAgent.cs
│   │   ├── SheepIdentity.cs
│   │   ├── SheepMember.cs
│   │   ├── MvpSheepSpawnDistributor.cs
│   │   ├── SpecialSheepMarker.cs
│   │   ├── SpecialSheepPool.cs
│   │   └── SpecialSheepSpawnPoint.cs
│   ├── Name/
│   │   ├── SheepNameGenerator.cs
│   │   └── SheepNamePool.cs
│   ├── Skills/
│   │   └── PoopAbility.cs
│   ├── Task/
│   │   ├── RecruitSheepTask.cs
│   │   ├── MvpTaskSystem.cs
│   │   ├── MvpHudView.cs
│   │   └── JoinToastView.cs
│   ├── UI/
│   │   ├── MvpResultPanelView.cs
│   │   ├── MvpCodexView.cs
│   │   ├── MvpTmpUiFont.cs
│   │   └── MvpUiFactory.cs
│   ├── GameFlow/
│   │   ├── MvpGameController.cs
│   │   └── MvpSessionStats.cs
│   ├── Camera/
│   │   └── CameraFollow2D.cs
├── Content/
│   ├── Data/SheepMvp/
│   │   └── DefaultSpecialSheepPool.asset
│   └── Perfabs/Sheep/
│       ├── SheepMember.prefab
│       ├── RecruitableSheep.prefab
│       ├── SpecialSheep_TopHat.prefab
│       ├── SpecialSheep_RedBow.prefab
│       ├── SpecialSheep_Horned.prefab
│       └── SpecialSheep_Black.prefab
└── Scenes/Old/Legacy/Level_01.unity

Assets/Art/SheepSprites/
├── Sheep_Normal.png
├── Sheep_Special_TopHat.png
├── Sheep_Special_RedBow.png
├── Sheep_Special_Horned.png
└── Sheep_Special_Black.png
```

当前主分支没有 asmdef，现有 `SceneLoader`、`AudioManager` 和 `PauseManager` 都属于 `Assembly-CSharp`。本 MVP 暂不新增 asmdef，以免新程序集无法引用这些现有类型。

当前仓库实际目录名是 `Perfabs`。本文暂时沿用该路径，避免同时出现 `Prefabs` 和 `Perfabs` 两套目录；后续如需纠正拼写，必须由目录 owner 在 Unity Project 窗口内统一移动并保留 GUID。

当前羊 Sprite 位于 `Assets/Art/SheepSprites`，以透明 PNG、Single Sprite、无 Mipmap 的方式导入。原始 JPG 保留为源素材；运行时 Prefab 只引用透明 PNG。

首轮功能已经在独立验证副本中通过导入检查，并由 `Level_01` Scene owner 集成到现有场景；未修改 `SampleScene` 或 `ProjectSettings/`。

## 5. 与现有系统的集成

- 重新开始优先调用 `SceneLoader.Instance.ReloadCurrentScene()`。
- 加入羊和拉屎音效调用 `AudioManager.Instance.PlaySFX(clip)`；直接运行 Dev Scene、实例不存在时静默跳过音效。
- `Level_01` 的 PauseManager 会设置 `Time.timeScale = 0`。玩家移动和拉屎技能必须在暂停时停止响应。
- HUD、加入提示和结算面板集成到现有 `GameCanvas` 下，不创建第二个 Canvas 或 EventSystem。
- MVP 完成时在 `Level_01` 内显示结算面板，暂不连接 Ending Scene。

## 6. 核心组件

### 6.1 FlockMovementController

负责族群级输入和中心移动，不挂在任何成员羊上：

- 从项目级输入中读取现有 `Player/Move` Input Action。
- `Update` 读取 `Vector2` 输入。
- 输入向量归一化，避免斜向加速。
- `FixedUpdate` 移动独立的 `SheepFlock` 中心对象。
- 成员羊不接收输入，而是根据族群中心与邻近成员自主转向。
- `Time.timeScale == 0` 时不处理移动。
- 游戏完成时允许关闭输入。

初始参数：

| 参数 | 默认值 |
|---|---:|
| 常态速度上限 | 4 |
| 加速度 | 12 |
| 减速度 | 16 |

`SheepFlock` 是逻辑控制对象，不是羊，也不需要 Sprite；它带有一个小型 Trigger，仅用于检测人数障碍并拒绝当前移动步。

### 6.2 RecruitableSheep

挂载在场景里的 5 只待招募羊上。

```csharp
public bool TryRecruit(FlockController flock);
```

- 首次接触返回 `true` 并加入族群。
- 已加入后再次接触返回 `false`。
- 接触对象只需是已登记的 `SheepMember`；不存在特定成员检测。
- 加入后登记为 `SheepMember` 并启用 `SheepFlockAgent`。
- 待招募羊由 `WildSheepWander` 在出生点附近缓慢徘徊，并随机切换为停步发呆；加入后关闭该组件并由 `SheepFlockAgent` 接管。
- 被狼打散后先完成击飞，停稳且仍未入队时恢复野生羊行为。
- 加入提示使用 `SheepIdentity.DisplayName`，例如 `“棉花糖”加入了族群！`。

招募判断通过组件引用完成，不依赖 Tag、对象名字或全局查找。

### 6.3 FlockController

负责维护动态成员集合，并向每个成员提供族群中心、整体目标速度和邻居列表。

```csharp
public int RecruitedCount { get; }
public int MemberCount { get; }

public event Action<RecruitableSheep, int> SheepRecruited;
public bool TryRecruit(RecruitableSheep sheep);
public bool Remove(SheepMember member);
```

- `RecruitedCount`：新找到的羊，范围为 0～5。
- `MemberCount`：当前族群成员数量，直接来自成员集合。
- 成功加入后只发出一次 `SheepRecruited` 事件。
- 成员没有固定槽位或固定相对位置，加入后从当前接触位置自然并入。
- 移除任何成员都不会导致输入、镜头或族群对象丢失。

运行时的独立 `SheepFlock` 对象只表示玩家操控的族群中心，不代表任何一只实体羊：

```text
SheepFlock
├── FlockMovementController
└── FlockController
```

成员通过轻量级局部转向规则围绕该中心运动，不使用 NavMesh。

### 6.4 SheepFlockAgent

- 每只成员使用共享空间网格查询局部邻居，再独立计算分离、凝聚和速度对齐，不追逐固定槽位。
- 归群边界使用面积近似守恒的动态椭圆：长轴平滑跟随整体移动方向，长宽比随成员数提高，并叠加低频轮廓起伏和小幅个体半径差异，避免规模增大后收敛成规则圆。
- 移动时叠加低频、连续变化的游走方向，使成员自然换位而不发生逐帧随机抖动。
- 延迟跟随只作用于速度大小；移动方向使用当前输入或中心实际方向，并在中心停止后丢弃尚未传播的历史尾巴，避免轻点输入延迟传到外围。
- 全群视觉朝向需持续约 0.1 秒的水平输入才确认；确认后各成员使用固定的 0.03～0.15 秒短延迟翻面，该延迟不按中心距离排序。idle、狼撞飞和障碍后弹仍由个体实际速度决定朝向。
- 族群中心的目标速度始终是主要移动方向；距离中心过远时逐渐增强归群力。
- 停止输入后移动中的游走归零，只保留超出舒适半径时的归群、距离过近时的避让，以及受统一数量预算控制的局部左右踱步。
- 未获得踱步预算或速度低于停止阈值的成员直接归零，避免整群持续晃动；移动输入、整群主动动作或离群会立即取消踱步。
- 羊数超过阈值后错峰重算转向，但每个物理帧仍执行速度平滑、障碍判断和位移，降低大羊群 CPU 峰值。
- 所有成员使用运动学 `Rigidbody2D` 和触发器，不参与实体推挤。

成员移动目标被实体障碍截断时会触发撞击反馈：可破坏障碍为短促挤压，当前不能破坏或没有破坏规则的障碍会触发后弹、变红和连续抖动。
上述成员表现不驱动全局镜头；当前 Alpha 的围栏镜头抖动只由 E 整群冲刺实际命中障碍时触发。
围栏冲撞使用确定峰值的衰减振荡，并按当前正交相机尺寸相对第一阶段进行缩放，保证不同羊群阶段具有接近的屏幕反馈强度。

### 6.5 FlockSizeObstacle

- 使用长方形 `SpriteRenderer` 和 Trigger，不依赖新增 Layer 或 Physics 2D 碰撞矩阵。
- 羊群数量小于 6 时调用 `FlockMovementController.RejectCurrentMovement()`，将族群中心退回本次移动前的位置。
- 羊群数量达到 6 时关闭 Trigger 并播放一次简单变色、放大后销毁的破坏反馈。
- 任意成员可以触发破坏，但只有羊群中心负责阻挡，保持操控对象仍是族群而非某只羊。

### 6.5.1 规则化障碍遭遇与花草反馈

`MvpObstacleEncounterSpawner` 在羊生成完成后，从原有 2～3 只普通羊群组中选择奖励点，在其周围生成两种易读的遭遇：

- U 形栅栏：三面封闭、开口朝向地图中心，实体碰撞阻挡羊；数量达到配置要求后接触自动撞碎。
- 木桶横排：五个木桶组成入口，任意已入队羊接触后逐个撞碎。
- 每组入口放置一小片花草。羊踩入 Trigger 后，花草先压扁、旋转、褪色，再散出数片带抛物线和旋转的花瓣。

遭遇直接复用本局已经生成的羊，不额外增加羊数，因此任务目标、随机名字和特殊羊数量仍由同一生成结果决定。奖励羊的出生点活动半径缩小为 `1.2`，避免玩家抵达前离开围栏区域。

### 6.6 SheepIdentity

每只初始成员羊和待招募羊都挂载 `SheepIdentity`：

```csharp
public string DisplayName { get; private set; }
public void AssignName(string displayName);
```

- 名称只在本局运行时写入，不保存到 Prefab 或 ScriptableObject。
- 名称用于加入提示和最终成员名单。
- 第一版不在羊头顶持续显示名称，避免增加世界空间 UI。

### 6.7 SheepNamePool 与 SheepNameGenerator

`SheepNamePool` 是只读 ScriptableObject 配置，策划可以在 Inspector 中编辑候选名称。建议初始提供至少 12 个名称：

```text
棉花糖、云朵、小卷、豆豆、奶盖、白团、咩咩、毛球、盐巴、小雪、软糖、月亮
```

`SheepNameGenerator` 在单局开始时为初始成员羊和 5 只待招募羊分配名称：

```csharp
public string NextUniqueName();
```

规则：

- 忽略空字符串和纯空格名称。
- 单局内优先保证名称不重复。
- 如果有效名称数量不足，使用 `小羊 01`、`小羊 02` 等后备名称。
- 重新开始关卡后重新随机。
- 第一版不要求固定随机种子，也不跨局保存名称。

### 6.8 RecruitSheepTask

纯 C# 任务对象，目标固定为 5，方便编写 EditMode 测试。

```csharp
public int Target { get; }
public int Progress { get; private set; }
public bool IsComplete => Progress >= Target;

public event Action<int, int> ProgressChanged;
public event Action Completed;
```

任务只接收成功招募结果，不直接处理碰撞或 UI。

### 6.9 PoopAbility

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
→ 检查存量、使用间隔和场上数量
→ 在 PoopSpawnPoint 生成 Poop.prefab
→ 消耗 1 格存量，空缺每 10 秒恢复 1 格
→ 播放简单动画或音效
→ HUD 显示剩余存量
→ 成功生成后统计拉屎次数加一
```

初始参数：

| 参数 | 默认值 |
|---|---:|
| 最大存量 | 5 格 |
| 单格恢复时间 | 10 秒 |
| 最短使用间隔 | 0.5 秒 |
| 粪便存在时间 | 5 秒（包含生成和消失动画） |
| 场上最大数量 | 2 个 |
| 外观概率 | 手绘 `shit.png` 93%，稀有卡通图 7% |
| 开局状态 | 满存量，可立即使用 |

只有成功生成粪便时才增加统计次数；存量不足、使用间隔内或场上已满时的无效按键不计数。暂停和游戏完成后不能释放。

第一版粪便没有伤害或生态效果，只验证主动技能链路。后续可以扩展施肥、减速、吸引生物或标记道路。

### 6.10 MvpSessionStats

`MvpSessionStats` 是普通运行时 C# 对象，不是 ScriptableObject，也不写入 PlayerPrefs。

```csharp
public int PoopUses { get; private set; }
public void RecordPoop();
public MvpResultSnapshot Complete(IReadOnlyList<SheepMember> members, int recruitedTotal);
```

统计规则：

- 每次成功生成 `Poop.prefab` 后记录一次拉屎。
- 计时使用未暂停的运行时间；暂停时间不会计入。
- 完成时从 `FlockController.Members` 生成不可变结算快照，包含名称、招募数、分数和用时。
- 重新加载关卡后全部重置，不跨局保存。

第一版结算字段保持简单：

```text
当前羊数：51
成功招募：50
当前分数：50
拉屎次数：12
游戏用时：01:43
本局同伴：云朵、豆豆、奶盖……
```

### 6.11 MvpGameController

负责连接羊群、任务和 UI：

- 根据实际成功生成的羊数创建 `MvpTaskSystem`，避免任务目标与场景数量脱节。
- 使用 `MvpSheepSpawnDistributor` 创建本局待招募羊，并使用 `SheepNameGenerator` 分配单局不重复名称。
- 创建并持有本局 `MvpSessionStats`。
- 监听 `FlockController.SheepRecruited`。
- 驱动任务进度、名称提示、统计和 HUD。
- 监听 PoopAbility 的成功释放事件并记录次数。
- 全部必做任务完成后只触发一次结算状态。
- 完成时停止统计计时、关闭玩家输入并显示结算面板。
- 结算面板返回标题时优先调用现有 `SceneLoader.Instance.LoadMainMenu()`。

### 6.12 UI

HUD 使用 uGUI，Canvas 参数：

- Render Mode：Screen Space - Overlay。
- Canvas Scaler：Scale With Screen Size。
- Reference Resolution：1920×1080。
- Match：0.5。

界面内容：

| 状态 | 任务 | 族群 | 技能 |
|---|---|---|---|
| 开局 | 必做/可选任务列表 | 族群：1 | Space：拉屎 |
| 加入后 | 实时更新各任务进度 | 族群：2 | Space：拉屎 |
| 恢复中 | 保持当前进度 | 保持当前数量 | 大便：4/5 |
| 完成 | 全部必做任务完成 | 实际族群数 | 打开本局统计 |

`JoinToastView` 每次招募时显示 `“{羊名}”加入了族群！`，持续 1.5 秒后淡出。

`MvpResultPanelView` 在完成时显示：

- `羊群集合完毕！`
- 族群规模、成功招募数和当前分数。
- 拉屎次数。
- 格式化后的游戏用时。
- 本局成员名称列表。
- 返回标题按钮。

`MvpCodexView` 是 MVP 占位实现，由暂停菜单进入，提供“当前同伴”和“羊图鉴”两个页签。名册读取真实成员与随机别名；图鉴当前按内容 ID 展示基础类型资料。特殊羊能力和完整卡片式图鉴仍属于后续内容设计。

### 6.13 MvpTaskSystem

任务系统使用普通 C# 定义和运行时状态，支持：

- `RecruitedTotal`、`CurrentFlockCount`、`PoopUses` 三种指标。
- 单任务多个条件以及 `All` / `Any` 条件模式。
- 必做与可选任务、首次出现标记、完成状态和进度快照。
- 全部必做任务完成事件；事件只触发一次。

当前任务由 `MvpGameController` 在每局开始时根据实际生成数量创建，避免额外维护一份可变运行时 ScriptableObject。

### 6.14 MvpSheepSpawnDistributor

生成器把 Scene 中预摆的待招募羊视为编辑参考并隐藏，每局重新实例化精确的目标数量：

- 普通羊按 1、2、3 只成组，权重可在 Inspector 中调整。
- 生成布局前先为每个生成位调用 `SpecialSheepPool.TryPick`；成功抽中的特殊羊始终单只放置，其余位置作为普通羊分组。
- 品质按彩蛋 0.1%、金色 0.3%、紫色 1%、蓝色 5%、绿色 50% 的绝对概率累计判定，未命中时生成普通羊。
- `SpecialSheepPool` 用本局已出现类型 ID 集合过滤候选。同一种特殊羊只会成功抽中一次；命中空品质或已抽空品质时返回失败，由生成器在该位置生成普通羊，不降级重抽。
- 先使用有效固定特殊羊点位，再为剩余已抽中特殊羊选择随机位置；固定点位只决定位置。
- 所有位置避开玩家安全区、Collider 和过密区域；随机布局失败时使用网格回退。
- `fixedSpawnSeed = 0` 时每局随机，非 0 时便于复现布局。

### 6.15 特殊羊扩展边界

`SpecialSheepMarker` 保存类型、品质以及预留的特效/技能 ID，并在生成和招募时通知同物体上实现 `ISpecialSheepFeature` 的组件。新增特殊羊时创建包含 `RecruitableSheep` 与该标记的 Prefab，再加入 `DefaultSpecialSheepPool.asset` 对应品质；具体特效和技能确定后以独立组件实现接口。

## 7. 场景结构

```text
Level_01
├── Main Camera
├── Global Light 2D
├── SheepMvp_WorldGrid（背景与坐标网格）
├── SheepMvp_SizeGate（6只羊可破坏）
├── SheepGameplay
│   ├── MvpGameController
│   ├── SheepFlock（输入、成员集合、技能）
│   └── Sheep_Initial（普通成员）
├── RuntimeRecruitableSheep（运行时创建）
│   ├── RecruitableSheep_XX（普通羊，1～3只成组）
│   └── SpecialSheep_XX（特殊羊，单只）
├── RuntimeObstacleEncounters（运行时创建）
│   ├── Encounter_XX_FenceCorral（U形栅栏、奖励羊、花丛）
│   └── Encounter_XX_BarrelGate（木桶横排、奖励羊、花丛）
├── GameCanvas（已有）
│   ├── SheepHUD
│   │   ├── TaskText
│   │   ├── FlockCountText
│   │   ├── PoopCooldownText
│   │   └── JoinToast
│   ├── MvpResultPanel（运行时占位 UI）
│   ├── MvpCodexPanel（运行时占位 UI）
│   └── PausePanel（已有）
├── PauseManager（已有）
└── EventSystem（已有）
```

当前 `Level_01` 的生成区域为 `100 × 50` 个世界单位；背景、移动边界和相机边界由同一配置同步。Scene 中可以添加 `SpecialSheepSpawnPoint` 作为固定特殊羊点位。

## 8. 数据流

```text
任意族群成员接触羊
→ RecruitableSheep.TryRecruit(flock)
→ FlockController.TryRecruit()
→ 登记 SheepMember
→ SheepRecruited 事件
→ MvpGameController
→ MvpTaskSystem 更新所有相关任务
→ MvpSessionStats 在结算时读取最终成员与数量
→ HUD 显示带名称的加入提示
→ 全部必做任务完成后进入结算状态
→ MvpResultPanelView 显示本局统计
```

```text
玩家按 Space
→ PoopAbility 检查存量、使用间隔和场上数量
→ 生成 Poop.prefab
→ MvpSessionStats 记录成功释放
→ 更新技能 UI
→ 5 秒内完成生成、停留和消失动画后销毁粪便
```

## 9. 配置

| 配置 | 编辑者 | 读取者 | 默认值 | 校验规则 | 运行时可变 |
|---|---|---|---|---|---|
| 生成总数 | 策划 | MvpGameController | Level_01：50 | 1～50；包含特殊羊 | 否 |
| 生成区域 | 策划/关卡 | MvpGameController | Level_01：100×50 | 两轴大于羊间距 | 否 |
| 普通羊组权重 | 策划/关卡 | MvpSheepSpawnDistributor | 60/30/10 | 非负且总和大于0 | 否 |
| 特殊品质概率 | 策划 | SpecialSheepPool | 50/5/1/0.3/0.1% | 0～100；按高品质到低品质累计判定 | 否 |
| 特殊羊池 | 策划/美术 | SpecialSheepPool | 绿色 4 个外观 Prefab | 忽略空项和重复类型；单局已出现类型不可再选 | 否 |
| 固定特殊点位 | 关卡 | SpecialSheepSpawnPoint | 空 | 必须位于有效区域；只控制位置 | 否 |
| 障碍遭遇数量 | 关卡 | MvpGameController | Level_01：5 | 0～12；受有效小羊群组数量限制 | 否 |
| 野生羊速度上限 | 策划/程序 | WildSheepWander | 0.62 | 大于等于0 | 否 |
| 野生羊活动半径 | 策划/程序 | WildSheepWander | 3.8 | 大于0；奖励羊运行时缩为1.2 | 本局初始化时可缩小 |
| 任务目标 | 程序 | MvpTaskSystem | 实际生成数 | 由成功生成结果计算 | 本局初始化时确定 |
| 名称池 | 策划/文案 | SheepNameGenerator | 至少12个名称 | 去空、去重，不足时使用后备名 | 否 |
| 族群常态速度上限 | 策划/程序 | FlockMovementController | 4 | 大于等于0 | 可被限时加速临时突破 |
| 族群加速度 | 策划/程序 | FlockMovementController | 12 | 大于等于0 | 否 |
| 族群减速度 | 策划/程序 | FlockMovementController | 16 | 大于等于0 | 否 |
| 成员最大速度 | 策划/程序 | SheepFlockAgent | 5.2 | 大于0 | 转向时变化 |
| 舒适半径 | 策划/程序 | SheepFlockAgent | 1.65 | 不小于避让半径 | 否 |
| 避让半径 | 策划/程序 | SheepFlockAgent | 1.15 | 大于等于0 | 否 |
| 游走强度 | 策划/程序 | SheepFlockAgent | 0.7 | 大于等于0 | 仅移动时生效 |
| 障碍破坏人数 | 策划/程序 | FlockSizeObstacle | 6 | 大于0 | 否 |
| 拉屎存量 | 策划 | PoopAbility | 5格 | 大于0 | 本局内恢复 |
| 单格恢复时间 | 策划 | PoopAbility | 10秒 | 大于0 | 否 |
| 最短使用间隔 | 策划 | PoopAbility | 0.5秒 | 大于等于0 | 否 |
| 粪便寿命 | 策划 | PoopAbility | 5秒 | 大于0 | 否 |
| 场上粪便上限 | 策划 | PoopAbility | 2 | 大于0 | 否 |

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

招募使用组件检测；显示层级暂时使用 `SpriteRenderer.sortingOrder`；现有 Build Settings 保持不变。

## 11. 实现与集成状态

1. 已实现 WASD 羊群移动、自定义相机跟随和动态世界边界。
2. 已实现成员羊、待招募羊、随机名称和单局去重。
3. 已实现可配置随机生成、1～3 只普通羊群组、按品质概率生成且单局去重的特殊羊池。
4. 已实现任意成员接触招募、任务列表、加入提示和 HUD。
5. 已实现 Space 拉屎、5 格存量与恢复、生成物限制和存量 UI。
6. 已实现 `MvpSessionStats`、结算面板和返回标题流程。
7. 已实现同伴名册和羊图鉴占位界面，并复用暂停菜单入口。
8. 已接入 `SceneLoader`、`AudioManager`、设置和暂停状态。
9. 已集成 `MainMenu → Level_01`，未修改 `ProjectSettings/`。
10. 已在独立 Unity 验证副本中完成 Sprite 导入、Prefab/Scene 引用、特殊池、生成规则和脚本编译检查。
11. 已实现野生羊徘徊/发呆、规则化栅栏与木桶奖励遭遇、两级撞击反应和花草踩踏动画。

## 12. 验收标准

- WASD 可以稳定控制羊群中心，输入不依赖任何成员羊。
- 斜向移动不会更快。
- 每只羊只能加入一次。
- 初始成员羊和本局生成的所有待招募羊都有非空名称。
- 单局羊名不重复；名称池不足时正确使用后备名称。
- 加入提示显示实际加入羊的名称。
- 修改生成总数后，实际生成数、任务目标和结算统计一致。
- 当前 `Level_01` 精确生成 50 只待招募羊，每个生成位按品质概率独立判定。
- 普通羊群组大小为 1～3，权重可调；特殊羊不与其他羊组成生成组。
- 羊群中心使用加减速移动，成员跟随中心的实际速度；速度始终受当前上限约束，限时加速可临时提高上限。
- 所有羊根据水平移动方向翻转；移动时有轻微步态，静止时持续呼吸，并由各自独立随机计时错峰触发压缩、拉伸和恢复的待机动作。
- 未入队羊在出生点附近缓慢徘徊，并会随机停下发呆；加入后立即交还羊群跟随逻辑。
- `Level_01` 每局最多生成 5 组规则化障碍遭遇，使用 U 形栅栏或木桶横排，并围绕已有 2～3 只奖励羊生成。
- 羊撞上可破坏障碍时有短促碰撞反馈；撞上当前无法破坏的实体障碍时有明显后弹、变红和抖动反馈。
- 羊踩到遭遇入口的花草时，花草会压扁、褪色并散出旋转花瓣后消失。
- 同一种特殊羊单局最多生成一次；命中空品质或抽空品质时该位置生成普通羊。
- 固定特殊点位优先用于已抽中特殊羊，剩余特殊羊继续随机放置。
- 特殊羊池可按品质增加 Prefab、类型 ID、特效 ID 和技能 ID，不需要修改生成器。
- 所有成员羊都能围绕族群中心自然换位；静止后只有受限数量的已招募羊在原地附近错峰踱步，其余成员不抖动。
- 移除任一成员后，族群输入和镜头仍保持有效。
- 族群数量不超过 5 时无法穿过人数障碍，达到 6 后接触可将其破坏。
- 背景网格不带 Collider，不参与招募或羊群运动。
- 加入提示每次只出现一次。
- Space 能生成粪便。
- 每 0.5 秒最多成功释放一次；存量最多 5 格，每 10 秒恢复 1 格。
- 场上粪便不超过 2 个，并在 5 秒内完成生成、停留和消失后销毁。
- 拉屎次数只统计成功生成的粪便。
- 游戏用时不包含暂停时间，并在完成后停止增加。
- 结算正确显示族群规模、拉屎次数、游戏用时和成员名单。
- 全部必做任务完成后只触发一次完成界面。
- 重新开始后羊、任务、UI 和技能状态全部重置。
- Unity Console 无编译错误或 Missing Reference。
- 本功能没有修改 `ProjectSettings/`。

## 13. 第一版不实现

- 主动冲撞、战斗和其他群体技能。
- 牛、猴子或其他物种。
- 生老病死、繁殖和疾病。
- 食物、饥饿和随机事件。
- 多关卡和人类结局。
- 名称存档、名称本地化和玩家自定义命名。
- 跨局数据保存、排行榜和长期统计。
- 通用技能框架、全局事件系统或复杂羊群算法。
