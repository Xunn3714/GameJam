# README

Unity 版本：**Unity 6.5（6000.5.9f1）**

本 README 用于总结当前已经完成并测试通过的 Game Jam 基础工程内容。

当前工程的基础框架已经完成，具体玩法方向现已确定为“俯视角羊群 MVP”。

实现玩法前请阅读：

* `docs/design/羊群MVP策划案.md`
* `docs/tech docs/羊群MVP技术方案.md`

已经完成的可复用基础能力包括：

* 主菜单
* 场景切换
* 暂停系统
* 设置系统
* 音频系统
* Ending / Credits

当前已经确定：

* 玩家控制对象：羊群整体，不存在固定头羊。
* 视角：2D俯视角。
* 操作：WASD移动，Space拉屎。
* MVP目标：接触并招募另外5只羊。
* 完成反馈：显示羊名、族群规模、拉屎次数和游戏用时。

最终关卡数量、最终美术、正式音频和 Level → Ending 的衔接仍待后续试玩决定。

\---

# 1\. 当前已完成系统

## 主菜单 MainMenu

当前已完成：

* 游戏标题
* START GAME
* EXIT GAME
* SET / Settings
* Settings 返回逻辑

基本流程：

```text
MainMenu
├─ START GAME → Level\_01
├─ SET → SettingPanel
└─ EXIT GAME
```

打开 Settings 时：

```text
GameTitle      OFF
MenuPanel      OFF
SET Button     OFF
SettingPanel   ON
```

返回主菜单时：

```text
GameTitle      ON
MenuPanel      ON
SET Button     ON
SettingPanel   OFF
```

\---

# 2\. 当前 Scene

当前 Scene：

```text
\_Game/Scenes/MainMenu.unity
\_Game/Scenes/Level\_01.unity
\_Game/Scenes/Ending.unity
```

当前 Build Profiles Scene List：

```text
0  MainMenu
1  Level\_01
```

`Ending.unity` 资产已经存在，但当前没有进入 `EditorBuildSettings` Scene List，且没有与 Level\_01 接通。

目前真正接通的流程：

```text
MainMenu
   ↓
Level\_01
```

Ending 当前已经做好，但**暂时不与 Level\_01 写死连接**。

原因：

目前还不知道最终会有几关。

以后可以根据实际情况变成：

```text
MainMenu
   ↓
Level\_01
   ↓
Ending
```

也可以是：

```text
MainMenu
   ↓
Level\_01
   ↓
Level\_02
   ↓
Level\_03
   ↓
Ending
```

所以当前没有提前绑定 Level → Ending。

\---

# 3\. 当前工程目录结构

现有工程结构保持不变，不随意重构队友目录。

当前主要结构：

```text
Assets
└── \_Game
    ├── Content
    │   ├── Art
    │   │   ├── Characters
    │   │   ├── Environment
    │   │   └── UI
    │   │
    │   ├── Audio
    │   │   ├── BGM
    │   │   └── SFX
    │   │
    │   ├── Data
    │   └── Perfabs              # 当前仓库实际拼写；更名需 owner 统一处理
    │       └── UI
    │
    ├── Runtime
    │   ├── Gameplay
    │   ├── Flock
    │   └── UI
    │       └── MainMenu
    │
    └── Scenes
        ├── MainMenu.unity
        ├── Level\_01.unity
        └── Ending.unity
```

除非团队统一决定，否则不要随意修改这套目录结构。

注意：根目录规范中的目标拼写是 `Prefabs`，但当前已提交资产位于 `Perfabs`。在 owner 统一更名前，新玩法资产暂时沿用实际目录，避免出现两套近似路径。

\---

# 4\. MainMenu 系统

## MainMenuController.cs

主要负责：

```text
ShowMenu()
ShowSettings()
BackToMenu()
StartGame()
QuitGame()
```

START GAME 当前通过：

```text
MainMenuController
    ↓
SceneLoader.LoadLevel01()
```

进入 `Level\_01`。

退出逻辑：

```text
Unity Editor
→ 停止 Play Mode

正式 Build
→ Application.Quit()
```

\---

# 5\. Pause 暂停系统

`Level\_01` 当前已经有完整 Pause 系统。

按键：

```text
ESC
```

逻辑：

```text
Playing
   ↓ ESC
Paused
   ↓ ESC
Playing
```

Pause UI：

```text
PAUSED

CONTINUE
MAIN MENU
SETTINGS
```

暂停时：

```csharp
Time.timeScale = 0f;
```

继续游戏时：

```csharp
Time.timeScale = 1f;
```

Pause 中的 Settings 复用了同一套设置系统。

推荐层级：

```text
PausePanel
├── PauseWindow
│   ├── PauseTitle
│   ├── Btn\_Continue
│   ├── Btn\_MainMenu
│   └── Btn\_Settings
│
└── SettingPanel
```

注意：

`SettingPanel` 必须能够独立显示和隐藏。

不要把 `SettingPanel` 放到一个打开 Settings 时会被关闭的父节点下面。

\---

# 6\. PausePanel 默认状态

进入游戏前：

```text
PausePanel = Inactive
```

必须默认关闭。

否则一进入 Level\_01 就会直接显示暂停界面。

\---

# 7\. Input System

项目当前使用 Unity 新版：

```text
Input System
```

Pause 当前使用：

```csharp
Keyboard.current.escapeKey.wasPressedThisFrame
```

不要使用旧版：

```csharp
Input.GetKeyDown(...)
Input.GetAxis(...)
```

玩家输入方向已经确定，输入应挂在羊群控制对象上，不挂在任何成员羊上。

羊群 MVP 使用：

```text
Player/Move → WASD
Player/Poop → Space（待新增）
```

羊群移动继续使用现有 `Assets/InputSystem_Actions.inputactions`，不要使用旧版 `Input.GetAxis`。具体实现与验收见羊群 MVP 技术方案。

\---

# 8\. Settings 设置系统

当前设置包含：

```text
BGM Volume
SFX Volume
```

Settings 当前可以从：

```text
MainMenu
Pause Menu
```

两处使用。

Slider 参数：

```text
Min Value      = 0
Max Value      = 1
Whole Numbers  = OFF
```

含义：

```text
0.0 = 0%
0.5 = 50%
1.0 = 100%
```

Slider 的 `On Value Changed` 必须绑定：

```text
Dynamic float
```

例如：

```text
BGM\_Slider
→ SettingsController.SetBGMVolume(float)

SFX\_Slider
→ SettingsController.SetSFXVolume(float)
```

不要选 Static Parameters 版本。

\---

# 9\. 音量保存

当前通过：

```text
PlayerPrefs
```

保存音量。

使用 Key：

```text
BGMVolume
SFXVolume
```

当前已测试：

```text
修改 BGM / SFX
   ↓
退出 Settings
   ↓
停止 Play
   ↓
再次 Play
   ↓
音量保持上一次设置
```

MainMenu → Level\_01 后音量也会继续保持。

\---

# 10\. AudioManager

当前已经有持久化：

```text
AudioManager
```

结构：

```text
AudioManager
├── AudioManager.cs
├── AudioSource #1 → BGM
└── AudioSource #2 → SFX
```

两个 AudioSource：

```text
Play On Awake = OFF
```

当前接口：

```text
PlayBGM(AudioClip)
StopBGM()
PlaySFX(AudioClip)

SetBGMVolume(float)
SetSFXVolume(float)
```

AudioManager 使用：

```csharp
DontDestroyOnLoad(gameObject);
```

因此可以跨 Scene 保留。

目前还没有正式 BGM / SFX 文件。

这是正常的。

以后音频资产建议放：

```text
\_Game/Content/Audio/BGM
\_Game/Content/Audio/SFX
```

不需要重新写音量系统。

\---

# 11\. SceneLoader

当前场景切换统一交给：

```text
SceneLoader
```

当前接口：

```text
LoadMainMenu()
LoadLevel01()
LoadScene(string sceneName)
ReloadCurrentScene()
```

SceneLoader 同样使用：

```csharp
DontDestroyOnLoad(gameObject);
```

并在切 Scene 前恢复：

```csharp
Time.timeScale = 1f;
```

尽量不要在其他脚本里到处重新写：

```csharp
SceneManager.LoadScene(...)
```

以后如果新增 Level\_02：

```csharp
SceneLoader.Instance.LoadScene("Level\_02");
```

即可。

\---

# 12\. Canvas 统一规范

MainMenu / Level\_01 / Ending 的 Canvas 尽量统一。

Canvas：

```text
Render Mode:
Screen Space - Overlay
```

Canvas Scaler：

```text
UI Scale Mode:
Scale With Screen Size

Reference Resolution:
1920 × 1080

Screen Match Mode:
Match Width Or Height

Match:
0.5
```

不要出现：

```text
MainMenu = Scale With Screen Size
Level = Constant Pixel Size
```

这种不一致情况。

否则 UI 在不同 Scene 中尺寸会发生变化。

\---

# 13\. Ending Scene

当前 Ending 已经完成基础结构，但暂时独立存在。

当前结构：

```text
Ending
├── Main Camera
├── Canvas
│   ├── CreditsArea
│   │   └── CreditsContent
│   │       ├── Credits Text
│   │       └── ...
│   │
│   └── EndingButtons
│       ├── Btn\_MainMenu
│       └── Btn\_Exit
│
├── EventSystem
└── EndingController
```

\---

# 14\. Credits 制作人员名单

Credits 使用多个独立的 TextMeshPro 文本块。

推荐结构：

```text
CreditsContent
├── Txt\_GameTitle
├── Txt\_ProjectManager
├── Txt\_GameDesign
├── Txt\_Programming
├── Txt\_2DArtist
├── Txt\_MusicDesigner
├── Txt\_SoundEffectDesigner
└── Txt\_Thanks
```

当前 Credits 类别包含：

```text
PROJECT MANAGER
GAME DESIGN
PROGRAMMING
2D-ARTIST
MUSIC DESIGNER
SOUND EFFECT DESIGNER
THANK YOU FOR PLAYING
```

\---

# 15\. Credits 滚动逻辑

当前滚动由：

```text
CreditsScroller.cs
```

控制。

最终需求：

```text
Credits 从下方开始
      ↓
缓慢向上滚动
      ↓
滚到编辑器中预设的最终位置
      ↓
停止
```

不是全部滚出屏幕。

现在：

```text
Ending Scene 中 CreditsContent 保存的位置
=
Credits 最终停止位置
```

因此可以把最终画面摆成：

```text
GAME TITLE

PROJECT MANAGER
...

THANK YOU FOR PLAYING
```

然后运行时 Credits 滚到这个位置后停止。

Credits 使用：

```csharp
Time.unscaledDeltaTime
```

所以不会受到：

```csharp
Time.timeScale
```

影响。

推荐速度：

```text
40–50  较慢
60     正常
80+    较快
```

\---

# 16\. Ending 右下角按钮

Ending 当前右下角固定：

```text
MAIN MENU →
EXIT GAME →
```

它们不会跟着 Credits 滚动。

功能：

```text
MAIN MENU →
    ↓
返回 MainMenu

EXIT GAME →
    ↓
退出游戏
```

目前按钮只是功能占位 UI。

以后美术可以替换：

* 按钮背景
* 字体
* 箭头 Sprite
* Hover 效果
* Pressed 效果

不需要修改按钮逻辑。

\---

# 17\. EndingController

EndingController 当前负责：

```text
ReturnToMainMenu()
QuitGame()
```

Unity Editor 中：

```text
QuitGame()
→ 停止 Play Mode
```

正式 Build 中：

```text
QuitGame()
→ Application.Quit()
```

如果 Ending 直接在 Editor 中 Play，而 SceneLoader 没有从 MainMenu 创建，也可以通过 fallback 返回 MainMenu。

\---

# 18\. 当前主要脚本

目前已经使用的基础脚本：

```text
MainMenuController.cs
PauseManager.cs
AudioManager.cs
SettingsController.cs
SceneLoader.cs
EndingController.cs
CreditsScroller.cs
```

\---

# 19\. 各脚本职责

## MainMenuController.cs

负责：

```text
主菜单 UI 状态
Settings 打开 / 返回
Start Game
Quit Game
```

## PauseManager.cs

负责：

```text
ESC Pause
Pause
Resume
Pause Settings
Back To Pause
Return To MainMenu
```

## AudioManager.cs

负责：

```text
BGM AudioSource
SFX AudioSource
BGM 播放
SFX 播放
音量控制
跨 Scene 保留
```

## SettingsController.cs

负责：

```text
初始化 Slider
控制 BGM
控制 SFX
保存 PlayerPrefs
读取 PlayerPrefs
```

## SceneLoader.cs

负责：

```text
统一场景切换
MainMenu
Level\_01
通用 LoadScene
Reload Current Scene
```

## CreditsScroller.cs

负责：

```text
Credits 向上滚动
滚到指定位置停止
使用 unscaled time
```

## EndingController.cs

负责：

```text
Ending → MainMenu
Ending → Quit
```

\---

不要为了“可能用得到”提前加系统。

\---

# 20. 标准测试流程

每次修改基础系统后建议测试：

```text
MainMenu
 ↓
SET
 ↓
修改 BGM / SFX
 ↓
RETURN
 ↓
START GAME
 ↓
Level\_01
 ↓
ESC
 ↓
SETTINGS
 ↓
RETURN
 ↓
CONTINUE
 ↓
ESC
 ↓
MAIN MENU
```

Ending 单独测试：

```text
Ending
├── Credits 正常出现
├── Credits 正常滚动
├── Credits 到最终位置后停止
├── MAIN MENU →
└── EXIT GAME →
```

确认：

```text
Console 没有红色 Error
MainMenu 可以正常进入
Level\_01 可以正常进入
Pause 不会自动出现
ESC 可以暂停
Continue 可以恢复
MainMenu Settings 正常
Pause Settings 正常
BGM 音量可以保存
SFX 音量可以保存
Ending 按钮没有超出屏幕
Ending MainMenu 正常
Ending Exit 正常
Credits 最终停在完整可见位置
```

\---

# 22\. Git / 协作注意事项

提交前：

```text
1. 停止 Play Mode
2. Ctrl + S 保存所有 Scene
3. 等 Unity 编译完成
4. 检查 Console 是否有红色 Error
5. 确认自己当前分支
6. 检查所有 Changes
7. Unity Asset 和 .meta 一起提交
8. 不提交缓存目录
```

不要提交：

```text
Library/
Temp/
Logs/
obj/
UserSettings/
```

除非团队有特殊要求。

Unity 的：

```text
Scene
Script
Prefab
Asset
Folder
```

对应的 `.meta` 文件应一起提交。
