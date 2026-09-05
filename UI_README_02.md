# Game Jam UI 新增任务 README

> 本文档只记录从本轮“新任务分割线”之后新增或调整的 UI 工作。重点包括：正式 UI 美术资源接入、PauseSystem Prefab 化、TaskSystem 任务面板、任务勾选逻辑，以及 BannerSystem 的特殊羊提示预留接口。

## 1. 本轮目标

- 接入美术组新提供的按钮、面板、图标资源
- 保留已有 Button / OnClick / Controller 逻辑，优先只替换 Source Image
- 将暂停页整理成可复用 PauseSystem Prefab
- 新增左上角 TaskSystem 任务入口与任务面板
- 第一只羊 / 第二只羊收集后自动切换任务完成图标
- TaskPanel 默认隐藏，通过任务图标打开
- TaskPanel 打开时任务图标隐藏，关闭后重新出现
- BannerSystem 默认隐藏，预留后续特殊羊触发接口

## 2. UI 美术资源接入

建议目录：

```text
Assets/_Game/Content/Art/UI/
├─ Buttons
├─ Icon
└─ Panels
```

Unity 导入建议：

```text
Texture Type      = Sprite (2D and UI)
Sprite Mode       = Single
Mesh Type         = Full Rect
Generate Mip Maps = Off
Alpha Is Transparency = On
```

完整按钮 / 图标优先：

```text
Image Type = Simple
Preserve Aspect = On（图标类）
```

需要被拉伸的通用面板或按钮，可根据实际需要使用：

```text
Image Type = Sliced
```

## 3. MainMenu 美术替换

主菜单继续沿用原有逻辑，不重新搭建按钮。

原则：

```text
保留 Button
保留 OnClick
保留 MainMenuController
只替换 Image → Source Image
```

当前开始按钮已开始使用新的 `530_195` 开始按钮美术。

按钮尺寸仍可能受到 `MenuPanel` 上 `Vertical Layout Group` 控制；如需单独控制高度，可使用：

```text
Layout Element
→ Preferred Height
```

## 4. PauseSystem Prefab

原暂停页最初直接存在于 `Level_01` 场景中。

推荐结构：

```text
GameCanvas
└─ PauseSystem
   ├─ PauseManager
   └─ PausePanel
      ├─ PauseWindow
      │  ├─ PauseTitle
      │  ├─ Btn_Continue
      │  ├─ Btn_MainMenu
      │  └─ Btn_Settings
      └─ SettingPanel
```

状态原则：

```text
PauseSystem = Active
PausePanel  = Inactive
```

`PauseManager` 必须位于一直 Active 的根节点上，才能持续监听 ESC。

### 4.1 PauseManager 自动按钮绑定

为避免 Prefab 化后 Button 引用仍指向场景旧对象导致断引用，暂停按钮改为由 `PauseManager` 在运行时自动绑定：

```text
Btn_Continue
Btn_MainMenu
Btn_Settings
SettingPanel / Btn_Back
```

对应逻辑：

```text
ContinueGame
ReturnToMainMenu
ShowSettings
BackToPause
```

这些按钮的 Inspector：

```text
Button → On Click()
```

可以保持：

```text
List is Empty
```

后续拖入其他关卡时无需重新接 OnClick。

## 5. TaskSystem

本轮新增左上角任务系统。

交互目标：

```text
平时只显示任务图标
点击图标 → 展开任务面板
打开面板后 → 任务图标隐藏
点击关闭按钮 → 面板关闭
关闭后 → 任务图标重新出现
```

推荐层级：

```text
GameCanvas
└─ SheepHUD
   ├─ JoinToast
   └─ TaskSystem
      ├─ Btn_TaskIcon
      └─ TaskPanel
         ├─ Btn_Close
         ├─ Txt_Title
         ├─ TaskText
         ├─ Flock Count Text
         ├─ TaskRow_01
         │  ├─ Check_Icon_01
         │  └─ Txt_Task_01
         └─ TaskRow_02
            ├─ Check_Icon_02
            └─ Txt_Task_02
```

`TaskSystem` 根节点使用全屏 Stretch：

```text
Anchor = Stretch / Stretch
Left   = 0
Right  = 0
Top    = 0
Bottom = 0
```

## 6. TaskPanel 内容

任务面板背景使用新的任务面板美术。

当前显示内容：

```text
Task

Find Sheep: 0/5
Group: 1

☐ Collide with one sheep
☐ Collide with more sheep
```

其中：

```text
TaskText
```

继续使用原有 `Find Sheep: X/5` 动态文字。

```text
Flock Count Text
```

继续使用原有 `Group: X` 动态文字。

本轮没有重写这两项玩法数据，只重新整理 UI 结构。

## 7. TaskRow

新增两个任务行容器：

```text
TaskRow_01
├─ Check_Icon_01
└─ Txt_Task_01
```

```text
TaskRow_02
├─ Check_Icon_02
└─ Txt_Task_02
```

`TaskRow` 本身只作为 RectTransform 容器，不需要 Image。

这样后续可整行移动、隐藏或更新。

## 8. TaskChecklistView

新增脚本：

```text
Assets/_Game/Runtime/UI/Level_UI/TaskChecklistView.cs
```

职责：

```text
监听 FlockController.SheepRecruited
→ 根据 RecruitedCount 更新两个任务图标
```

规则：

```text
开局
□ Task 01
□ Task 02

收集第 1 只羊
☑ Task 01
□ Task 02

收集第 2 只羊
☑ Task 01
☑ Task 02
```

使用的 Sprite：

```text
Unchecked Sprite
→ 任务未完成图标

Checked Sprite
→ 任务打勾图标
```

`TaskChecklistView` 不负责羊的招募，只监听现有招募事件。

当前采用运行时自动寻找 `FlockController` 的方式，减少不同关卡的手动引用。

## 9. TaskPanelToggle

新增脚本：

```text
Assets/_Game/Runtime/UI/Level_UI/TaskPanelToggle.cs
```

引用：

```text
Task Panel
→ TaskPanel

Task Button
→ Btn_TaskIcon

Close Button
→ TaskPanel / Btn_Close
```

初始状态：

```text
Open On Start = Off
```

交互：

```text
开局
Btn_TaskIcon 显示
TaskPanel 隐藏

点击 Btn_TaskIcon
→ TaskPanel 显示
→ Btn_TaskIcon 隐藏

点击 Btn_Close
→ TaskPanel 隐藏
→ Btn_TaskIcon 重新显示
```

两个按钮的：

```text
Button → On Click()
```

均可保持空，由 `TaskPanelToggle` 自动绑定。

## 10. TaskSystem Prefab

TaskSystem 按独立模块设计，可保存为：

```text
Assets/_Game/Content/Prefabs/UI/TaskSystem.prefab
```

Prefab 内保留：

```text
Btn_TaskIcon
TaskPanel
TaskPanelToggle
TaskChecklistView
任务完成 / 未完成 Sprite 引用
```

以后其他关卡可直接：

```text
GameCanvas / SheepHUD
→ 拖入 TaskSystem.prefab
```

无需重新搭 TaskPanel。

注意：`Find Sheep` 与 `Group` 的真实文字更新仍沿用当前玩法侧已有 HUD 数据链。

## 11. BannerSystem

现有结构：

```text
GameCanvas
└─ BannerSystem
   ├─ Icon
   └─ BannerText
```

本轮不额外增加父级。

Banner 当前用途调整为：

```text
特殊羊 / 特殊事件提示
```

例如：

```text
Find another Sheep!
```

当前要求：

```text
游戏开局默认不显示 Banner
只有收集到特殊羊等事件后才显示
```

当前特殊羊玩法尚未完成，因此本轮只预留：

```text
Show(...)
ShowTemporary(...)
Hide()
```

等显示接口，不接真实特殊羊触发。

## 12. 当前 Level UI 结构

```text
GameCanvas
├─ PausePanel / PauseSystem
├─ SheepHUD
│  ├─ JoinToast
│  └─ TaskSystem
├─ BannerSystem
└─ ResultPanel
```

本轮 UI 不主动修改稳定玩法对象本身。

## 13. 本轮完成状态

| 内容 | 状态 |
|---|---|
| 新 UI 美术资源导入 | 已开始接入 |
| MainMenu 开始按钮新美术 | 已接入 |
| Pause 按钮新美术 | 已接入 |
| PauseSystem Prefab 化 | 已整理 |
| PauseManager 自动按钮绑定 | 已调整 |
| TaskPanel 背景 | 已接入 |
| Task 入口图标 | 已接入 |
| TaskPanel 点击打开 / 关闭 | 已完成 |
| TaskPanel 打开时隐藏入口图标 | 已完成 |
| TaskPanel 关闭后恢复入口图标 | 已完成 |
| TaskRow_01 / TaskRow_02 | 已完成 |
| 第一只羊任务打勾 | 已完成逻辑 |
| 第二只羊任务打勾 | 已完成逻辑 |
| TaskSystem Prefab | 已整理为可复用结构 |
| Find Sheep 动态文字 | 沿用已有逻辑 |
| Group 动态文字 | 沿用已有逻辑 |
| Banner 默认隐藏 | 已确定 |
| 特殊羊 Banner 接口 | 已预留 |
| 特殊羊真实逻辑 | 待玩法 |
| 最终正式 UI 排版 | 待美术联调 |

## 14. 本轮开发原则

```text
已有逻辑不重写
美术替换不破坏 Button / Controller
Prefab 尽量内部自包含
减少 Scene 手动引用
UI 只负责显示
玩法系统继续负责真实数据
未知玩法不提前写死
```

## 15. 下一步

```text
1. 等特殊羊玩法完成
2. 接入 BannerSystem 特殊羊触发
3. 根据最终玩法调整 Task 文案
4. 根据美术稿继续替换 Pause / Task / Result UI
5. 将 PauseSystem / TaskSystem 迁移至正式关卡
6. 最终测试不同分辨率下 Anchor
7. Build 前统一检查 Prefab 引用
```

## 16. 当前结论

本轮新增工作主要完成：

```text
正式 UI 美术接入
PauseSystem 模块化
TaskSystem 模块化
任务入口展开 / 收起
第一只 / 第二只羊任务完成状态
Banner 特殊羊提示接口预留
```

后续不需要重新搭整套任务 UI，只需继续接真实玩法条件和正式美术资源。
