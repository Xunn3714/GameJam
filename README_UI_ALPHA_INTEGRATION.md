# Alpha UI 集成说明

## 范围

本次改动只负责把新 UI 美术和交互接入最新 `main` 玩法，不替换玩法实现。

玩法基线由 `main` 决定：

- WASD 移动。
- E 向当前左右朝向蓄势冲刺。
- Q 收束羊群。
- 出生羊圈、外围围栏、出口、阶段扩张和狼群规则均保留 `main` 版本。
- 外围出口在本局历史最高羊数达到 100 时解锁；真正撞开时，当前羊数仍需达标。

VFX、VFX 测试场景、额外特殊羊 Prefab 不在本次 UI 集成范围内。

## UI 结构

`AlphaFlockExpansion` 只有一个 `GameCanvas`、一个 `EventSystem` 和一个 `PauseManager`：

```text
GameCanvas
├─ TaskSystem
│  ├─ Btn_TaskIcon
│  └─ TaskPanel
├─ PauseSystem
│  └─ PausePanel
│     ├─ PauseWindow
│     ├─ SettingPanel
│     └─ CollectionPanel
├─ AlphaBanner
├─ SheepHUD
├─ JoinToast
└─ WolfEventHud
```

`PauseSystem` 根节点始终激活，`PausePanel` 开局默认隐藏，以便 `PauseManager` 持续监听 ESC。

## 任务面板

真实数据链：

```text
AlphaFlockExpansionController.RefreshObjectives()
    → MvpHudView.UpdateObjectives()
    → TaskChecklistView.ApplyObjectives()
```

四项固定任务与 `main` 对齐：

| ID | 显示内容 | 完成条件 |
|---|---|---|
| `alpha.pen` | 撞开出生羊圈 | 羊圈真正打开 |
| `alpha.exit_unlock` | 壮大羊群并解锁出口 | 历史最高羊数达到出口门槛 |
| `alpha.escape` | 撞开外围围栏并逃离 | 越过打开的外围出口 |
| `alpha.special` | 招募一只特殊羊 | 本局特殊羊数达到 1 |

任务数据为空或重置时，所有任务行会恢复未完成状态，不保留上一局的进度。

## Banner

`AlphaBannerView` 统一负责排队、替换和淡入淡出；`BannerView` 只负责文字和可选图标。

- `Show(message)`：普通消息排队。
- `Show(message, icon)`：带图标消息排队。
- `ShowLatest(message)`：替换已过期的动态提示。
- `ShowLatest(message, icon)`：替换动态提示并显示图标。

出生羊圈的人数不足/可撞开提示继续使用 `ShowLatest`，不会被过滤。招募特殊羊时，Banner 显示该羊当前 `SpriteRenderer` 上的图片。

## 暂停菜单

`PauseManager` 保留旧接口，并增加新 UI 按钮绑定：

- `OpenPause` / `PauseGame`
- `ResumeGame` / `ContinueGame`
- `ShowSettings` / `BackToPause`
- `ShowCollection` / `BackFromCollection`
- `RestartGame`
- `ReturnToMainMenu`
- `ExitGame`

返回主菜单和重开关卡优先复用 `SceneLoader`。结算界面显示后，暂停、设置、图鉴和任务面板都会关闭，ESC 不再打开暂停菜单。

## 场景所有权

`AlphaFlockExpansionSceneSetup` 是 Alpha 场景的可重复生成入口：

- 直接实例化 `TaskSystem`、`PauseSystem` 和 `BannerSystem` Prefab。
- 自动绑定任务、暂停、Banner 和玩法 Controller 引用。
- 不再打开或复制归档的 `Level_01`。
- 重复运行后仍只保留一套 UI 系统。

## 验收清单

1. WASD、E 冲刺、Q 收束与 `main` 一致。
2. 冲刺期间羊朝左右冲刺方向，不会旋转。
3. 四个任务的进度、勾选和羊群数实时更新。
4. 出生羊圈动态提示正常刷新。
5. 特殊羊 Banner 显示正确文字和图片。
6. ESC、Continue、Settings、Sheep、Restart、Main Menu 和 Exit 正常。
7. Task、Pause、Collection 和 Result 不会互相叠加。
8. `MainMenu → AlphaFlockExpansion` 流程正常。
9. 重新运行 Alpha Scene Setup 后场景引用仍然完整。
