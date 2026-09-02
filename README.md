# GameJam Unity Starter

面向 Game Jam 的 Unity 2D 协作工程。当前仓库只提供稳定的工程基线；具体玩法、输入动作、Layer、Prefab 和运行时模块应在题目与目标平台确定后按实际需求加入。

## 当前基线

- Unity：`6000.5.9f1`
- 渲染：Universal Render Pipeline / 2D Renderer
- 输入：Unity Input System（当前仍是模板 action asset）
- 当前入口：`Assets/Scenes/SampleScene.unity`（临时场景，首个竖切片完成时再改名和迁移）
- 目标平台：待确定

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

- `docs/meeting summary/`：历史会议记录。
- `docs/API docs/`：设计草案；只有明确标为 Accepted 且对应实现已存在的内容才可视为契约。
- `docs/tech docs/`：研究资料，不自动代表本项目采用该方案。
