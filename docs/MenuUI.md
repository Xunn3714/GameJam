# 菜单 UI

菜单 UI 是游戏专用模块，位于 `Assets/_Game/Runtime/UI/`。不依赖共享 `GameJamKit`，可以独立放到任意场景验证。

## 创建开发场景

在 Unity 顶部菜单选择 **GameJam → UI → Create Menu UI Dev Scene**。它会用 Unity 的场景 API 创建 `Assets/_Game/Scenes/Dev/MenuUiDev.unity`，不会修改现有 `SampleScene`。

若需要把 UI 加进当前已打开的个人开发场景，选择 **GameJam → UI → Create Menu UI In Active Scene**。同一场景已经存在 `MenuUIController` 时工具会停止，以避免重复创建。

## 可配置内容

- `GameMenuUI/Menu UI Controller`
  - `On Start Game`：绑定进入关卡、初始化存档等游戏逻辑。
  - `Bgm Audio Source`：给此 AudioSource 指定 `AudioClip`；设置页面的 BGM 按钮会切换静音状态并更新文案。
  - `Demonstration Popup`：直接编辑标题、正文和每个 option 的文本；每个 option 也可以绑定自己的 `On Selected` 事件。
- `GameMenuUI/BackgroundMotion`
  - 设定 `Background Texture` 可获得持续 UV 滚动。纹理 Import Settings 必须设为 **Wrap Mode: Repeat**。
  - 设定 `Background Sprite` 可获得缓慢的全景平移。两种背景源互斥。
  - `Texture Scroll Speed`、`Sprite Pan Distance` 和 `Shake Pixels Per World Unit` 都可在 Inspector 调整。
- `Main Camera/Camera Shake 2D`
  - 可从任意游戏逻辑调用 `Shake()` 或 `Shake(duration, magnitude)`。
  - 背景会读取同一个组件的偏移，因此即使 Canvas 使用 Screen Space Overlay，背景仍会随镜头晃动。
- `GameMenuUI/Popup Dialog`
  - `Default Content` 是可复用弹窗的标题、正文和多个选项配置。
  - 运行时也可以调用 `PopupDialog.Show(PopupContent)` 显示另一组内容。

## 验收流程

打开 `MenuUiDev` 后进入 Play Mode，验证开始游戏、设置、BGM 开关、返回和弹窗选项。为 BGM Audio Source 指定音频后，按钮可立即控制播放静音。
