# 文档状态说明

仓库文档分为四类：

- `design/`：具体玩法目标、规则、范围和验收标准。
- `meeting summary/`：历史记录，只说明当时讨论过什么。
- `API docs/`：设计草案。除非文档明确标为 **Accepted**，并且仓库中存在对应实现和验证，否则不得把示例 API 当成已确定契约。
- `tech docs/`：当前技术方案和研究资料；只有被当前策划案/实现入口引用且状态有效的文档才约束实现。

## 当前实现入口

羊群 MVP 开发按以下顺序阅读：

1. [`AGENTS.md`](../AGENTS.md)：仓库规则。
2. [羊群 MVP 策划案](design/羊群MVP策划案.md)：玩法事实来源。
3. [羊群 MVP 技术方案](<tech docs/羊群MVP技术方案.md>)：当前技术设计。
4. [`README_GameFramework.md`](../README_GameFramework.md)：已有基础系统。

历史会议、研究资料和 Planned API 只能作为背景，不能覆盖以上已确认的当前方案。

推荐状态：

- **Planned**：只有目标或问题描述，尚未确定设计。
- **Draft**：正在评审，允许大幅修改。
- **Accepted**：已确认并有对应实现负责人。
- **Implemented**：实现和验证均已进入仓库。
- **Deprecated**：保留历史背景，不再采用。

新增文档应写明状态、owner、最后更新时间和关联实现路径。不要创建空白占位文件；尚无内容时在 TODO 中记录即可。
