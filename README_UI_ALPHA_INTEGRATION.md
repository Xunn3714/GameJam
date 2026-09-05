# UI / Alpha Scene Integration README

## 本次改动概览

本次主要完成了 Alpha 羊群扩张场景中的 UI 接入与暂停系统整理，重点包括：

- Task 任务面板接入 Alpha 真实任务进度
- 特殊羊提示 Banner 接入并显示对应羊图片
- 暂停菜单视觉重排
- 暂停菜单新增 Sheep / Restart / Exit 功能
- 暂停菜单接入羊图鉴
- 图鉴打开时隐藏 Task UI，避免界面重叠
- 清理旧左上角透明 HUD 显示

## 1. Task 任务面板

### 目标

将原本左上角透明 HUD 中的 Alpha 任务进度，接入新的 TaskPanel UI。

### 当前真实任务

任务来源：

`Assets/_Game/Runtime/GameFlow/AlphaFlockExpansionController.cs`

由 `RefreshObjectives()` 生成：

1. `alpha.pen`
   - 凑够羊，按 E 撞开羊圈
2. `alpha.exit_unlock`
   - 羊群壮大到指定数量
3. `alpha.escape`
   - 撞开外围围栏，冲出草原
4. `alpha.special`
   - 招募一只特殊羊（支线）

### 数据链

```text
AlphaFlockExpansionController.RefreshObjectives()
        ↓
MvpHudView.UpdateObjectives(...)
        ↓
TaskChecklistView
        ↓
TaskRow_01 ~ TaskRow_04
```

### TaskPanel 当前结构

```text
TaskSystem
├ Btn_TaskIcon
└ TaskPanel
   ├ Btn_Close
   ├ Txt_Task
   ├ TaskRow_01
   │  ├ Check_Icon_01
   │  ├ Txt_Task_01
   │  └ Progress_01
   ├ TaskRow_02
   │  ├ Check_Icon_02
   │  ├ Txt_Task_02
   │  └ Progress_02
   ├ TaskRow_03
   │  ├ Check_Icon_03
   │  ├ Txt_Task_03
   │  └ Progress_03
   ├ TaskRow_04
   │  ├ Check_Icon_04
   │  ├ Txt_Task_04
   │  └ Progress_04
   ├ Image
   └ GroupCountText
```

### 行为

- `Progress_xx` 显示真实 `Progress / Target`
- `Check_Icon_xx` 根据 `IsComplete` 自动切换
- `GroupCountText` 显示当前羊群数量
- 第一个任务即使达到 `6/6`，也必须真正撞开羊圈后才会勾选
- 特殊羊任务在成功招募特殊羊后变为 `1/1` 并打勾

### 旧 HUD 处理

旧的：

```text
GameCanvas
└ SheepHUD
   ├ HudBackdrop
   ├ TaskText
   └ Flock Count Text
```

已隐藏对应显示对象，但保留 `SheepHUD` 本体及 `JoinToast`，避免影响其他提示逻辑。

## 2. AlphaBanner / 特殊羊提示

### 目标

特殊羊加入时，在现有 `AlphaBanner` 中显示：

```text
[对应特殊羊图片]  Special Sheep Joined!
```

而不是新增第二套 Banner。

### 当前链路

```text
FlockController.SheepRecruited
        ↓
AlphaFlockExpansionController.HandleSheepRecruited()
        ↓
读取被招募特殊羊 SpriteRenderer.sprite
        ↓
AlphaBannerView.Show(message, sprite)
        ↓
BannerView.Show(message, icon)
```

### 关键修改

`AlphaBannerView` 支持：

```csharp
Show(string message, Sprite icon)
```

`AlphaFlockExpansionController` 在特殊羊招募时传入该羊实际 Sprite。

`BannerView` 不再在 `Awake()` 中直接：

```csharp
gameObject.SetActive(false);
```

避免把和它挂在同一个对象上的 `AlphaBannerView` 一起关闭。

### 出生羊圈提示处理

出生羊圈尚未打开时，普通 Banner 不显示，避免 TutorialPen 提示反复闪烁。

特殊羊图片 Banner 不受该限制。

羊圈打开后，以下普通提示恢复正常：

- 阶段升级
- 狼群提示
- 羊圈打开
- 出口解锁
- 围栏破坏

## 3. PauseSystem UI 重排

### 当前结构

```text
PauseSystem
└ PausePanel
   ├ PauseWindow
   │  ├ PauseTitle
   │  ├ Btn_Continue
   │  ├ Btn_MainMenu
   │  ├ Btn_Settings
   │  ├ Btn_Sheep
   │  ├ Btn_Exit
   │  └ Btn_Restart
   ├ SettingPanel
   └ CollectionPanel
```

### 视觉调整

- 使用新的暂停菜单大背景图
- `PauseWindow` 作为中央主面板
- 主按钮重新排版
- `Restart` 放置在左上角
- `Exit` 使用独立退出按钮
- 保留 `SettingPanel` 与 `CollectionPanel` 作为暂停菜单内二级页面

## 4. PauseManager 功能

### 已接功能

```text
Continue
→ 继续游戏

Main Menu
→ 返回 MainMenu

Settings
→ 打开 SettingPanel

Sheep
→ 打开 CollectionPanel

Restart
→ 重新加载当前场景

Exit
→ 退出游戏
```

### Restart

调用现有 `RestartGame()`：

优先：

```csharp
SceneLoader.Instance.ReloadCurrentScene();
```

否则：

```csharp
SceneManager.LoadScene(SceneManager.GetActiveScene().name);
```

### Exit

Editor 中停止 Play；Build 中调用 `Application.Quit()`。

## 5. Collection / 羊图鉴接入

MainMenu 原有 `CollectionPanel` 已整理为可复用对象并接入 PauseSystem。

`CollectionPanelController` 在 `OnEnable()` 时会自动：

```csharp
RefreshCollection();
```

因此打开图鉴时会自动刷新当前图鉴数据。

### 暂停菜单行为

```text
PauseWindow
    ↓ Btn_Sheep
CollectionPanel 打开
PauseWindow 隐藏
TaskSystem 隐藏
```

返回：

```text
CollectionPanel 关闭
PauseWindow 恢复
TaskSystem 恢复
```

这样避免左上角 Task 图标 / TaskPanel 和图鉴发生 UI 重叠。

## 6. 当前测试结果

已确认：

- TaskPanel 能显示 Alpha 真实任务进度
- 羊群数量实时更新
- 四个任务勾选逻辑正常
- 羊圈任务在真正撞开后才完成
- 特殊羊任务可正常完成
- 特殊羊 Banner 能显示对应特殊羊 Sprite
- Pause 基本按钮可正常使用
- Sheep 按钮可以打开图鉴
- Collection 返回按钮可回到暂停菜单
- Restart 按钮已接当前关卡重开逻辑
- Exit 已接退出逻辑
- Task UI 在图鉴打开时可隐藏，避免遮挡

## 7. 主要涉及脚本

```text
Assets/_Game/Runtime/GameFlow/AlphaFlockExpansionController.cs
Assets/_Game/Runtime/UI/Alpha/AlphaBannerView.cs
Assets/_Game/Runtime/UI/.../BannerView.cs
Assets/_Game/Runtime/Task/MvpHudView.cs
Assets/_Game/Runtime/.../TaskChecklistView.cs
Assets/_Game/Runtime/.../PauseManager.cs
Assets/_Game/Runtime/.../CollectionPanelController.cs
```

> 注：部分具体目录以当前项目实际目录为准。

## 8. 提交前建议检查

1. 开局 PausePanel 不应自动显示
2. ESC 正常打开 Pause
3. Continue 正常继续
4. Settings 可打开并返回
5. Sheep 可打开图鉴并返回
6. 打开图鉴时 TaskSystem 不遮挡
7. Restart 能重新加载当前场景
8. Exit 正常
9. 收羊后 Task 进度实时变化
10. 撞开羊圈后第一项任务打勾
11. 收到特殊羊后：
    - `alpha.special = 1/1`
    - Task 打勾
    - 右上角弹出对应特殊羊图片 Banner
12. 出生羊圈阶段普通 Banner 不持续闪烁

## PR Summary

```text
- Integrated Alpha objective progress into the custom Task UI
- Added 4 objective rows with progress and completion states
- Reused AlphaBanner for special sheep notifications with dynamic sheep sprites
- Suppressed repeated normal banner messages before tutorial pen opens
- Reworked Pause UI layout
- Added Sheep Collection, Restart and Exit actions to Pause menu
- Integrated CollectionPanel into PauseSystem
- Hid TaskSystem while CollectionPanel is open to avoid UI overlap
```
