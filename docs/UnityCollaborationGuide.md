\# Unity Collaboration Guide



\## Unity Version



统一使用：



`6000.5.9f1`



不要使用其他 Unity 版本打开并保存项目。



\## Branch Workflow



开发前：



1\. 更新本地 main

2\. 切回自己的功能分支

3\. 将最新 main 合并到自己的分支

4\. 开始开发



普通开发不要直接提交到 main。



\## Unity Project Configuration



以下文件属于项目级配置，不应在普通功能开发中随意覆盖：



\- `Packages/manifest.json`

\- `Packages/packages-lock.json`

\- `ProjectSettings/ProjectVersion.txt`

\- `ProjectSettings/ProjectSettings.asset`



如果必须修改：



1\. 先通知技术组

2\. 说明修改原因

3\. 合并后重新打开 Unity

4\. 等待 Package Resolve 和脚本编译完成

5\. 确认 Console 无编译错误



\## Package Merge Warning



曾出现 UI 分支覆盖项目 Package 配置，导致：



\- Input System 缺失

\- URP / UGUI 配置缺失

\- Unity 进入 Safe Mode



因此合并 PR 前，如果发现修改中包含：



\- `Packages/`

\- `ProjectSettings/`



必须额外确认。



\## Scene / Prefab



\- 同一时间不要多人修改同一个 Scene

\- 同一 Prefab 尽量只有一个负责人编辑

\- `.meta` 文件必须与对应 Unity 资产一起提交

\- 资源移动、重命名和删除优先在 Unity Project 窗口内完成



\## Do Not Commit



不要提交：



\- `Library/`

\- `Temp/`

\- `Logs/`

\- `Obj/`

\- `UserSettings/`

\- 本机构建产物

