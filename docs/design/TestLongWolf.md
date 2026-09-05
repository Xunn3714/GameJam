# TestLongWolf 开发场景

- 状态：独立玩法原型，按 2026-09-05 的长狼需求制作。
- 范围：`Assets/_Game/Scenes/Dev/TestLongWolf.unity`；不代表正式关卡规则更新。
- 场景由 `LongWolfTestSceneSetup` 使用 Unity API 从已保存的 TestWolf 复制生成。

## 玩法

沿用 TestWolf 的起始 10 只羊、WASD 移动、狼群节奏 HUD、羊数归零 Game Over、R 重开。
单独使用 `Content/Perfabs/Wolf/LongWolf.prefab`：狼有长条身体，预警锁定后直线冲锋。
冲锋及逃离期间，身体实际扫到的全部有效羊群成员都会被带走；不撞散、不提供救援。
被带走成员立即退出羊群并关闭碰撞，沿狼身排成一串，随狼离场销毁。
路线外的羊不受影响；不会在预警时提前删除整条路径上的羊。

## 参数

以下参数由策划/程序在 Prefab Inspector 编辑，组件运行时读取，默认不在单局中改变：

| 组件 | 参数 | 新原型默认值 | 校验 |
|---|---|---:|---|
| LongWolfSweep | Body Length | 7 世界单位 | 至少 0.5 |
| LongWolfSweep | Body Width | 1.3 世界单位 | 至少 0.1；同时决定预警宽度 |
| Wolf | Warning Duration | 1.2 秒 | 非负 |
| Wolf | Charge Speed | 14 | 非负 |
| Wolf | Aim Follows Flock During Warning | false | 预警出现即锁定路线 |

生成与休息参数仍使用场景中的 WolfSpawner / WolfEventDirector，场景限制同时一只狼。
身体扫掠使用每只羊的 CircleCollider2D 中心及缩放后的半径，并覆盖两个物理帧之间的路段。
普通 Wolf 不挂 LongWolfSweep 时继续使用原来的撞散/叼一只逻辑。

## 编辑器入口与验证

- `Game Jam > Wolf Test > Create TestLongWolf Scene`：首次创建；已存在时只打开，不覆盖调参。
- `Game Jam > Wolf Test > Open TestLongWolf Scene`：单独打开场景，再按 Play。
- `Game Jam > Wolf Test > Validate TestLongWolf`：在已保存的场景状态下运行专用验证。

验证结果输出到 `Tools/LongWolf-validation.txt`，画面到 `Tools/LongWolf-preview.png`。
验证使用临时运行时摆位检查高速多目标命中、路线外安全、暂停、全灭、重开，并重载后运行自然节奏。
测试摆位和速度覆盖不保存回场景或 Prefab；验证结束停在 TestLongWolf 编辑状态。
