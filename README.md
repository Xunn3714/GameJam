# GameJam Unity Starter

面向 Game Jam 的 Unity 2D 协作工程。工程基线、主菜单、暂停、设置、音频和 Ending 已存在；俯视角羊群 MVP 原型已经集成到 `Level_01`。

> 项目另有一个不修改 `Level_01` 的暴力扩张实验场景：
> `Assets/_Game/Scenes/Dev/AlphaFlockExpansion.unity`。实验规则见
> [`羊群暴力扩张 Alpha`](docs/design/羊群暴力扩张Alpha.md)。

## 当前基线

- Unity：`6000.5.9f1`
- 渲染：Universal Render Pipeline / 2D Renderer
- 输入：Unity Input System；WASD 控制羊群，Space 使用拉屎技能
- 当前入口：`Assets/_Game/Scenes/MainMenu.unity`
- 当前关卡：`Assets/_Game/Scenes/Level_01.unity`
- 首轮目标平台：Windows（WASD + Space）

## 当前玩法方向

玩家控制一个最初只有 1 名成员的羊群，通过任意成员接触地图中的羊扩充族群。游戏中没有固定头羊；WASD 移动羊群中心，成员围绕中心动态跟随；Space 用于测试“拉屎”主动技能。地图会按配置生成精确数量的羊，每个生成位按品质概率决定普通羊或特殊羊；普通羊以 1～3 只成组分布，特殊羊单独出现。完成必做任务后显示族群规模、成员名单、分数、拉屎次数和游戏用时。

`Level_01` 当前原型配置为：`100 × 50` 世界区域、共生成 50 只待招募羊；特殊品质概率为绿色 50%、蓝色 5%、紫色 1%、金色 0.3%、彩蛋 0.1%，未命中或命中空池时生成普通羊。普通羊群组权重为 `60 / 30 / 10`。

## 当前 MVP 原型完成情况

- `MainMenu → Level_01` 开始流程、暂停和设置闭环。
- 随机且单局不重复的中文羊名，以及中文 TMP 字体随项目分发。
- 可配置的随机羊群生成、玩家安全区、密度与间距约束。
- 普通羊 1～3 只分组；特殊羊按品质概率单独生成，同一种特殊羊单局只出现一次。
- 任务列表、加入提示、同伴名册、羊图鉴占位界面和本局结算统计。
- 普通羊、礼帽羊、蝴蝶结羊、角羊和黑羊的透明 Sprite 与 Prefab。

开始实现前依次阅读：

1. [`AGENTS.md`](AGENTS.md)
2. [羊群 MVP 策划案](docs/design/羊群MVP策划案.md)
3. [羊群 MVP 技术方案](<docs/tech docs/羊群MVP技术方案.md>)

`ProjectSettings/ProjectVersion.txt` 是 Unity 版本的唯一事实来源。不要使用其他 Unity 版本打开并保存项目。

## 第一次拉取

1. 安装上面指定的 Unity Editor 版本。
2. 在仓库根目录运行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Tools\Setup-UnityGit.ps1
   ```

3. 通过 Unity Hub 打开仓库根目录，等待首次导入完成。
4. 确认 Console 没有编译错误，再创建自己的功能分支。

初始化脚本只配置当前仓库的 Git LFS 和 Unity Smart Merge，不修改全局 Git 配置。

## 日常开发

- 从最新 `main` 创建短生命周期分支，例如 `feat/player-input`、`fix/ui-scaling`。
- 一次 PR 只处理一个明确模块；不要顺手重排无关 Scene、Prefab 或 Project Settings。
- 同一时间一个 Scene 或 Prefab 只由一位 owner 编辑。
- 功能开发优先在个人 Dev Scene 验证，以 Prefab/独立模块形式交付给集成场景 owner。
- Unity Asset 的文件与 `.meta` 必须一起新增、移动和删除；移动资源优先在 Unity Project 窗口内完成。
- 不提交 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 或本机构建产物。

完整规则见 [AGENTS.md](AGENTS.md)。这些规则同时适用于人工成员、Codex、Claude Code 等 coding agent。

## 本地验证

关闭正在使用该目录的 Unity Editor 后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-UnityProject.ps1
```

出现第一批 EditMode 测试后，可以附加 `-RunTests`：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Validate-UnityProject.ps1 -RunTests
```

日志和测试结果写入被 Git 忽略的 `Logs/`。

## 目录方向

目录按需求渐进创建，不提交仅用于占位的空目录：

```text
Assets/
├─ GameJamKit/            # 已被实际玩法验证、可跨项目复用的能力
│  ├─ Runtime/
│  ├─ Editor/
│  └─ Tests/
├─ _Game/                 # 本次游戏专用内容
│  ├─ Runtime/
│  ├─ Content/
│  │  ├─ Art/
│  │  ├─ Audio/
│  │  ├─ Data/
│  │  └─ Prefabs/
│  └─ Scenes/
│     ├─ Main.unity
│     └─ Dev/
├─ Settings/              # URP、渲染等项目资产
└─ ThirdParty/            # 必须直接放在 Assets 下的第三方内容
```

不要预先创建通用 Manager、Event Bus、Addressables、对象池、存档或网络层。先完成一个可玩的竖切片，再把已经稳定的部分提取到 `GameJamKit`。

## 文档

- `AGENTS.md`：Codex、Claude Code 及其他 coding agent 的统一实现入口。
- `docs/design/`：已确认玩法目标、规则和 MVP 范围。
- `docs/meeting summary/`：历史会议记录。
- `docs/API docs/`：设计草案；只有明确标为 Accepted 且对应实现已存在的内容才可视为契约。
- `docs/tech docs/`：当前技术方案和研究资料；是否采用以文档状态及策划案/实现入口的引用为准。

当前仓库实际存在误拼目录 `Assets/_Game/Content/Perfabs/`。不要自行创建并行的 `Prefabs/` 或直接移动现有资产；由 owner 在 Unity Project 窗口中统一处理。
