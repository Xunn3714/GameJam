# Repository Rules for Humans and Coding Agents

本文件是仓库内工程协作规则的唯一事实来源，适用于人工成员、Codex、Claude Code 及其他 coding agent。

## 当前项目入口

当前已确认的玩法是“羊群 MVP”。涉及玩法、玩家、关卡、UI、输入、音效或数据的任务，开始前必须依次阅读：

1. `README.md`：当前工程状态和入口。
2. `docs/design/羊群MVP策划案.md`：玩法目标、规则、文案和范围。
3. `docs/tech docs/羊群MVP技术方案.md`：组件、数据流、文件范围和验收标准。
4. `README_GameFramework.md`：现有主菜单、暂停、音频和场景切换能力。

当前固定范围：2D俯视角、WASD移动、接触招募另外5只羊、单局随机且不重复的羊名、Space拉屎，以及完成时的简单本局统计。未经需求更新，不加入冲撞、敌人、饥饿、随机事件、其他物种、存档或长期统计。

实现时复用现有 `SceneLoader`、`AudioManager`、`PauseManager` 和 `Level_01/GameCanvas`。不创建第二个 EventSystem，不为羊群 MVP 新增 asmdef，不修改 `ProjectSettings/`。功能先在个人 Dev Scene 验证，再由 `Level_01` owner 集成。

发生文档冲突时：本文件的仓库规则优先；策划案决定“做什么”；技术方案决定“怎么做”；历史会议、研究资料和 Planned API 草案只能作为背景。

## 开始任务前

1. 确认当前 branch 和 `git status`，保留所有不属于当前任务的已有改动。
2. 阅读 `README.md`、相关模块文档以及目标目录下更具体的说明。
3. 明确本次改动的文件范围。涉及 Scene、Prefab、Input Actions、Project Settings 或共享 ScriptableObject 时，先确认 owner。
4. 不在同一个工作目录同时运行多个 Unity Editor。并行 agent 使用独立 Git worktree。

## Unity 资产安全

- 不读取、编辑或提交 `Library/`、`Temp/`、`Logs/`、`Obj/`、`UserSettings/` 和构建产物。
- Unity Asset 与对应 `.meta` 是一个不可分割的变更单元。
- 资源移动和重命名优先在 Unity Project 窗口完成，以保留 GUID。
- 不手工大范围修改 `.unity`、`.prefab`、`.asset` YAML；确需修改时必须做 Unity 导入和引用验证。
- 不无故执行全项目 Reimport 或 Reserialize Assets。
- SerializedField 改名必须使用 `FormerlySerializedAs`，并检查相关 Prefab/Scene。
- 不直接编辑自动生成文件；应修改其源文件并通过原工具重新生成。

## 模块边界

- `_Game` 只能依赖 `GameJamKit`；`GameJamKit` 不得反向依赖 `_Game`。
- 契约放在拥有该概念的模块附近，例如 `Combat/IDamageable`、`Interaction/IInteractable`，不要建立全局 `Interfaces` 杂物目录。
- ScriptableObject 用于由策划或美术编辑的静态配置，不作为共享的可变运行时状态。
- 运行时状态、存档和 API payload 使用普通可序列化 C# 类型。
- 优先使用 Inspector 引用、构造参数或窄接口；避免通过对象名字、全局 `Find`、Service Locator 或万能单例建立隐式依赖。
- 只有经过真实玩法验证且有第二个使用者的代码，才移动到 `GameJamKit`。
- asmdef 按稳定依赖边界创建，不按每个小文件夹拆分。

## Scene 与 Prefab

- 集成 Scene 必须有单一 owner。其他成员在个人 Dev Scene 开发，以 Prefab 或模块形式交付。
- 同一时间不要让两个 branch 修改同一个 Scene 或 Prefab。
- Prefab 应能在 Dev Scene 中独立放置和验证，不依赖某个固定名字的 Scene 对象。
- 优先组合组件；只有确实共享稳定结构时才创建 Prefab Variant。
- 不提前建立全局 Prefab Registry、Addressables 加载层或多层 Prefab 继承树。

## 配置与 API

- 新增配置前先写清楚：谁编辑、谁读取、默认值、校验规则以及运行时是否可变。
- 外部 API 出现后再为具体用例建立窄接口和 adapter；不要预建 Repository/缓存/网络框架。
- 设计文档中的示例签名不是已实现契约。文档只有在状态为 Accepted 且存在对应实现/测试时才具有约束力。

## 提交与交接

- 一个 branch/PR 处理一个主题，避免夹带无关格式化或资产移动。
- Commit message 使用清楚的英文动词摘要，例如 `feat(input): add jump buffering`。
- PR 必须说明：完成内容、改动资产、验证方式、已知风险、Scene/Prefab owner 交接。
- 不推送、合并、强制更新远端或重写公开历史，除非用户明确要求。

## 验证要求

- 纯 C# 逻辑优先写 EditMode 测试；依赖场景生命周期的行为使用少量 PlayMode 测试。
- 修改资源导入、Project Settings、asmdef 或包依赖后，必须完成一次干净的 Unity 导入/编译验证。
- 修改 Prefab 或 Scene 后，在 Unity 中打开并运行相关 Dev/集成 Scene。
- 任务结束时报告实际执行的验证；不能执行的验证必须说明原因。
