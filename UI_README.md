# Game Jam UI README

> 本文档为当前 UI 模块的统一说明，涵盖主菜单、设置、统计、图鉴、关卡内 UI、Ending / Credits 及相关数据接口。  
> 当前原则：**先完成可复用 UI 结构与接口，不在玩法未确定前提前写死数据或过度扩展系统。**

---

# 1. 当前 UI 工作范围

目前 UI 工作分为两部分：

## 1.1 主菜单 / 全局 UI

主要开发场景：

```text
Assets/_Game/Scenes/MainMenu.unity
```

当前已覆盖：

- Main Menu
- Settings
- Collection / 羊羊图鉴
- Statistics / 统计
- Developers 按钮占位
- Ending / Credits
- 通用 Audio / Scene / Stats / Collection 数据接口

## 1.2 关卡内 UI

当前开发 / 联调场景：

```text
Assets/_Game/Scenes/Old/Tests/TestLevel.unity
```

当前已有：

- PausePanel
- SheepHUD
- Join Toast
- BannerSystem
- ResultPanel

后续玩法与 UI 联调完成后，再将可复用 UI Prefab 迁移到：

```text
Assets/_Game/Scenes/Old/Legacy/Level_01.unity
```

---

# 2. MainMenu 当前结构

当前主菜单核心结构：

```text
Canvas
├─ Background
├─ Btn_Settings
├─ MenuPanel
│  ├─ Btn_Start
│  ├─ Btn_Collection
│  ├─ Btn_Statistics
│  └─ Btn_Quit
├─ SettingPanel
├─ Btn_Developers
├─ StatisticsPanel
└─ CollectionPanel
```

说明：

- `MenuPanel` 主要用于统一控制右侧主菜单按钮显示 / 隐藏。
- `Btn_Developers` 单独放在 Canvas 下，便于保持左下角独立布局。
- `Btn_Settings` 单独放在右上角。
- `SettingPanel` / `StatisticsPanel` / `CollectionPanel` 均作为独立页面切换显示。

---

# 3. MainMenuController

相关脚本：

```text
Assets/_Game/Runtime/UI/MainMenu/MainMenuController.cs
```

当前职责：

```text
ShowMenu()
ShowSettings()
ShowCollection()
ShowStatistics()
BackToMenu()
StartGame()
QuitGame()
```

页面切换原则：

```text
打开子页面
→ 隐藏 MenuPanel
→ 隐藏 Btn_Settings
→ 隐藏 Btn_Developers
→ 显示对应 Panel
```

返回主菜单：

```text
BackToMenu()
→ 关闭 Setting / Collection / Statistics
→ 恢复 MenuPanel
→ 恢复 Btn_Settings
→ 恢复 Btn_Developers
```

当前主菜单按钮：

```text
START GAME
COLLECTION
STATISTICS
EXIT GAME
```

额外按钮：

```text
SET
DEVELOPERS
```

---

# 4. Settings / 设置页面

当前 Settings 使用可复用 Prefab：

```text
Assets/_Game/Content/Prefabs/UI/SettingPanel.prefab
```

当前包含三条音量控制：

```text
BGM
SFX
Sheep
```

对应 UI 结构当前为：

```text
SettingPanel
├─ Title
├─ Main_Label
├─ Main_Slider
├─ Music_Label
├─ Music_Slider
├─ Sheep_Label
├─ Sheep_Slider
└─ Btn_Back
```

> 当前部分 Label 名称仍属于占位命名，最终文字可由美术 / 策划统一。

## 4.1 AudioManager

相关脚本：

```text
Assets/_Game/Runtime/Gameplay/AudioManager.cs
```

当前支持三路 AudioSource：

```text
BGM Source
SFX Source
Sheep Source
```

公开接口：

```text
PlayBGM(AudioClip)
StopBGM()
PlaySFX(AudioClip)
PlaySheepSFX(AudioClip)

SetBGMVolume(float)
SetSFXVolume(float)
SetSheepVolume(float)
```

## 4.2 SettingsController

相关脚本：

```text
Assets/_Game/Runtime/UI/MainMenu/SettingsController.cs
```

当前负责：

- 初始化 BGM Slider
- 初始化 SFX Slider
- 初始化 Sheep Slider
- 修改三路音量
- 使用 PlayerPrefs 保存 / 恢复音量

当前保存 Key：

```text
BGMVolume
SFXVolume
SheepVolume
```

Slider 范围：

```text
Min = 0
Max = 1
Whole Numbers = OFF
```

---

# 5. Statistics / 统计页面

当前 Statistics 页面采用：

```text
StatisticsPanel
└─ StatisticsWindow
   ├─ Txt_Title
   ├─ Btn_Back
   ├─ StatsScrollView
   │  ├─ Viewport
   │  │  └─ Content
   │  └─ Scrollbar Vertical
   └─ DetailPanel
```

当前左侧列表支持：

- Vertical Scroll
- Scrollbar Vertical
- 动态 Content 高度
- 动态 StatRow 生成

不再手工一条一条搭统计项。

---

# 6. StatRow Prefab

Prefab：

```text
Assets/_Game/Content/Prefabs/UI/StatRow.prefab
```

结构：

```text
StatRow
├─ Txt_Name
└─ Txt_Value
```

用途：

```text
左侧：统计名称
右侧：统计数值
```

示例：

```text
累计收集的羊                  326 只
```

相关脚本：

```text
StatRowView.cs
```

职责：

```text
Setup(statName, statValue)
```

Statistics UI 不需要知道具体有哪些统计项。

---

# 7. GameStatsManager

相关脚本：

```text
Assets/_Game/Runtime/Gameplay/GameStatsManager.cs
```

当前为通用统计数据层。

支持：

```text
RegisterStat()
AddStat()
SetStat()
UpdateMaxStat()
UpdateMinStat()
GetStat()
GetAllStats()
```

支持的数据类型：

```text
Integer
Decimal
TimeSeconds
```

支持显示：

```text
数值
单位后缀
时间格式
显示顺序
```

例如：

```text
累计收集的羊
累计破坏的栅栏
最大羊群数量
最小羊群数量
最快通关时间
最慢通关时间
```

后续玩法脚本只需要注册并上报数据，不需要修改 Statistics UI。

## 7.1 动态统计原则

例如：

```text
sheep_collected
fence_broken
npc_hit
clear_count
```

玩法层只需要：

```text
RegisterStat(...)
AddStat(...)
```

Statistics 页面自动：

```text
GetAllStats()
→ Instantiate StatRow.prefab
→ 显示 Name / Value
```

这样以后增加统计数据时，不需要重新在 Unity 里复制 UI 行。

---

# 8. StatisticsPanelController

相关脚本：

```text
StatisticsPanelController.cs
```

当前职责：

```text
打开 StatisticsPanel
→ 清空旧 StatRow
→ 读取 GameStatsManager
→ 动态生成 StatRow
→ 自动刷新显示
```

Content 在编辑状态下应保持为空：

```text
Content
```

运行时才生成：

```text
StatRow(Clone)
StatRow(Clone)
...
```

---

# 9. Collection / 羊羊图鉴

当前 Collection 页面结构：

```text
CollectionPanel
└─ CollectionWindow
   ├─ Txt_Title
   ├─ Txt_Progress
   ├─ Btn_Back
   ├─ SheepScrollView
   │  ├─ Viewport
   │  │  └─ Content
   │  └─ Scrollbar Vertical
   └─ DetailPanel
      ├─ SheepImage
      ├─ Txt_SheepName
      ├─ Txt_Count
      ├─ Txt_Description
      ├─ Txt_AbilityName
      ├─ Txt_AbilityDescription
      └─ Sheep_Sentence
```

说明：

- 左侧为可滚动羊卡片 Grid。
- 右侧为选中羊详情。
- `Sheep_Sentence` 当前为 Image，用于显示每只羊对应的独立句子 / 装饰图。
- 图鉴羊数量当前不写死，等待美术 / 玩法确定最终羊种。

---

# 10. SheepCard Prefab

Prefab：

```text
Assets/_Game/Content/Prefabs/UI/SheepCard.prefab
```

结构：

```text
SheepCard
├─ SheepImage
├─ Txt_Name
└─ Txt_Count
```

根节点包含：

```text
Image
Button
SheepCardView
```

用途：

```text
羊图片
羊名字
遇到次数
是否解锁
```

未解锁状态显示：

```text
???
尚未解锁
```

已解锁状态显示：

```text
普通羊
遇到过 X 次
```

`Button.OnClick()` 不需要手动绑定。

点击事件由 `SheepCardView` 在运行时动态接入。

---

# 11. SheepCardView

相关脚本：

```text
SheepCardView.cs
```

主要接口：

```text
Setup(
    Sprite,
    SheepName,
    EncounterCount,
    Unlocked,
    ClickAction
)
```

职责：

- 设置羊图片
- 设置名字
- 设置遇到次数
- 切换解锁 / 未解锁状态
- 接入动态点击事件

---

# 12. SheepCollectionDatabase

相关脚本：

```text
SheepCollectionDatabase.cs
```

通过 ScriptableObject 保存每种羊的固定资料。

每条羊数据包含：

```text
Sheep Id
Display Name
Icon
Description
Ability Name
Ability Description
Sentence Image
Display Order
```

数据资产：

```text
Assets/_Game/Content/Data/SheepCollectionDatabase
```

当前不提前写死最终羊数量。

后续每增加一种羊，只需要在 Database 中新增一条数据，不需要复制 Collection UI。

---

# 13. SheepCollectionManager

相关脚本：

```text
SheepCollectionManager.cs
```

职责：

```text
记录某只羊是否解锁
记录遇到次数
保存 / 读取图鉴进度
统计已解锁数量
提供总羊数量
```

主要接口：

```text
EncounterSheep(sheepId)
IsUnlocked(sheepId)
GetEncounterCount(sheepId)
GetUnlockedCount()
GetTotalCount()
GetAllSheep()
```

图鉴进度通过 PlayerPrefs + JSON 保存。

---

# 14. CollectionPanelController

相关脚本：

```text
CollectionPanelController.cs
```

职责：

```text
打开 CollectionPanel
→ 读取 SheepCollectionDatabase
→ 读取 SheepCollectionManager
→ 自动生成 SheepCard
→ 更新 Unlocked X / X
→ 点击羊卡
→ 显示右侧详情
```

右侧可显示：

```text
羊图片
羊名字
遇到次数
描述
能力名称
能力描述
Sentence Image
```

Content 在编辑状态下保持为空。

运行时才动态生成：

```text
SheepCard(Clone)
SheepCard(Clone)
...
```

---

# 15. SheepIdentity

相关脚本：

```text
Assets/_Game/Runtime/Flock/SheepIdentity.cs
```

当前保留原有：

```text
DisplayName
AssignName()
```

用于现有动态羊名 / Join Toast。

同时新增：

```text
sheepId
```

用于图鉴识别羊种。

说明：

```text
DisplayName
→ 本局随机 / 展示名字

sheepId
→ 羊种固定 ID
```

两者用途不同，不互相替代。

---

# 16. FlockController 与图鉴 / 统计接入

相关脚本：

```text
Assets/_Game/Runtime/Flock/FlockController.cs
```

当前原有招募流程保持：

```text
TryRecruit()
→ AddMember()
→ CompleteRecruitment()
→ RecruitedCount++
→ SheepRecruited
```

在确认招募成功后新增：

```text
SheepCollectionManager.EncounterSheep(sheepId)
```

用于：

```text
首次招募 → 解锁图鉴
再次招募 → EncounterCount + 1
```

同时接入：

```text
GameStatsManager
→ sheep_collected + 1
```

原有 Formation、HUD、Join Toast、SheepRecruited 链路不应被破坏。

---

# 17. Join Toast / Tip

现有羊加入提示继续保留：

```text
GameCanvas
└─ SheepHUD
   └─ JoinToast
```

相关脚本：

```text
Assets/_Game/Runtime/Task/JoinToastView.cs
```

当前调用链：

```text
FlockController.SheepRecruited
→ MvpGameController
→ JoinToastView.Show(sheepName)
```

例如：

```text
Test joined the group!
```

其中名字根据实际羊动态变化。

本轮图鉴功能没有替换这条逻辑。

---

# 18. BannerSystem

当前结构：

```text
GameCanvas
└─ BannerSystem
   ├─ Icon
   └─ BannerText
```

相关脚本：

```text
Assets/_Game/Runtime/UI/Level_UI/BannerView.cs
```

当前接口：

```text
SetText()
SetIcon()
Show()
Hide()
```

Banner 只负责 View。

当前暂不实现：

- Banner 队列
- Banner 去重
- 自动任务判断
- Persistent / Temporary 状态
- 完整动画系统

---

# 19. ResultPanel

当前 Victory / Defeat 共用同一个结算面板。

结构：

```text
GameCanvas
└─ ResultPanel
   └─ ResultWindow
      ├─ ResultTitle
      ├─ ResultDescription
      ├─ SheepCountLabel
      ├─ ScoreLabel
      ├─ RecruitCountLabel
      ├─ LostCountLabel
      ├─ TimeLabel
      └─ Btn_ReturnTitle
```

相关脚本：

```text
Assets/_Game/Runtime/UI/Level_UI/ResultPanelView.cs
```

当前仅保留显示接口。

目前尚未正式接入：

```text
Victory State
Defeat State
Score
Lost Sheep Count
Elapsed Time
Return MainMenu Flow
```

这些等待真实玩法数据完成后再接。

---

# 20. Victory / Defeat 原则

Victory / Defeat 使用同一个：

```text
ResultPanel
```

后续仅根据结果切换：

```text
ResultTitle
ResultDescription
统计数据
美术内容
```

不复制两套独立结算 UI。

结算页只保留：

```text
返回标题
```

Restart 继续由 Pause Menu 负责。

---

# 21. Ending / Credits

当前 Ending Scene 已有：

```text
Credits
MAIN MENU
EXIT GAME
```

Credits 使用多段 TMP 内容，而不是单个超长 TMP。

原因：

单个超长 TMP 在滚动时曾出现：

```text
文字拉丝
文字变形
渲染异常
```

Credits 当前逻辑：

```text
从下方开始滚动
→ 到编辑器预设最终位置
→ 停止
```

不会全部滚出屏幕。

相关脚本：

```text
CreditsScroller.cs
EndingController.cs
```

Ending 当前暂时不写死接到 Level_01，等待最终关卡数量确定。

---

# 22. 当前 TestLevel UI 结构

当前主要关卡内 UI：

```text
GameCanvas
├─ PausePanel
├─ SheepHUD
│  ├─ TaskText
│  ├─ Flock Count Text
│  └─ JoinToast
├─ BannerSystem
└─ ResultPanel
```

`TestLevel` 继续作为当前 UI / 玩法联调场景。

最终应优先迁移 Prefab，不重新搭一套。

---

# 23. 当前 UI Prefab

当前主要可复用 UI Prefab 包括：

```text
SettingPanel.prefab
StatRow.prefab
SheepCard.prefab
BannerSystem
ResultPanel
```

后续美术正式资产出来后，应优先替换：

```text
Image
Sprite
Font
Button Style
Decoration
```

不要为了换美术重写 UI 数据逻辑。

---

# 24. 本轮不继续扩展的内容

当前暂时不继续实现：

```text
DevelopersPanel 正式页面
图鉴最终羊数量
全部羊图鉴数据
所有 Statistics 项
Banner 队列
Tip 队列
完整 Victory / Defeat 判断
正式 Score 规则
正式 Lost Sheep 规则
最终 BGM / SFX
正式 UI 美术
```

原因：

这些依赖后续：

```text
玩法定稿
美术出图
真实游戏数据
```

当前先保留稳定接口。

---

# 25. 当前完成状态

| 内容 | 状态 |
|---|---|
| MainMenu 基础流程 | 已完成 |
| MainMenu 新布局 | 已完成结构调整 |
| Settings BGM 音量 | 已完成 |
| Settings SFX 音量 | 已完成 |
| Settings Sheep 音量 | 已完成 |
| Pause / Settings 复用 | 已有 |
| Statistics Scroll UI | 已完成 |
| StatRow Prefab | 已完成 |
| GameStatsManager | 已完成基础数据层 |
| Statistics 动态生成接口 | 已完成 |
| Collection Scroll / Grid | 已完成 |
| SheepCard Prefab | 已完成 |
| SheepCollectionDatabase | 已完成 |
| SheepCollectionManager | 已完成 |
| CollectionPanelController | 已完成基础接口 |
| SheepIdentity sheepId | 已接入 |
| FlockController → Collection | 已接入 |
| FlockController → sheep_collected Stat | 已接入 |
| 图鉴最终羊数据 | 待美术 / 玩法 |
| DevelopersPanel | 待后续 |
| Join Toast 动态羊名 | 已有，可用 |
| Banner 静态 UI | 已完成初版 |
| BannerView | 已完成 |
| Victory / Defeat Result UI | 已完成初版 |
| ResultPanelView | 已完成 |
| Result 真实玩法数据 | 待玩法 |
| Ending / Credits | 已完成基础结构 |
| 正式 UI 美术 | 待美术 |
| 最终分辨率测试 | 待最终联调 |

---

# 26. 后续接入建议

下一轮继续 UI 时，优先顺序：

```text
1. 等美术提供正式 UI 资源
2. 替换 MainMenu / Settings / Statistics / Collection 美术
3. 根据最终羊种填写 SheepCollectionDatabase
4. 将真实玩法事件继续上报 GameStatsManager
5. 完成 Statistics 正式统计项
6. 完成 ResultPanel 真实数据绑定
7. 完成最终关卡 → Ending
8. 将 TestLevel 可复用 UI 迁移至 Level_01
9. 做最终分辨率与 Build 测试
```

---

# 27. 当前开发原则

本轮继续遵循：

```text
不重复造 UI
不提前写死玩法数据
不为了“可能会用”增加复杂系统
UI View 与玩法判断分离
统计数据与显示分离
图鉴固定资料与玩家进度分离
正式美术出来后替换资源，不重写逻辑
```

---

# 28. 当前结论

目前本人的 UI / 基础界面任务已经基本完成。

当前已经具备：

```text
主菜单基础入口
Settings 三路音量
Statistics 动态统计框架
Collection 动态图鉴框架
Join Toast
Banner
ResultPanel
Ending / Credits
```

其中 Statistics 与 Collection 已经预留动态数据接口，不需要后续手工复制大量 UI。

现阶段不继续补写未知统计项、未知羊种或未确定的玩法数据。

下一阶段应主要进入：

```text
玩法联调
→ 美术资源替换
→ 数据接入
→ 最终 Scene 迁移
→ Build / 分辨率测试
```
