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
| LongWolfSweep | Body Length | 7 世界单位 | 至少 0.5；关闭自适应或无有效正交镜头时的后备长度 |
| LongWolfSweep | Scale Length To Screen | true | 按随机覆盖时长和冲锋开始时的镜头计算长度 |
| LongWolfSweep | Min / Max Coverage Seconds | 0.8 / 1.2 秒 | 非负，上限不小于下限；每只出场独立随机一次 |
| LongWolfSweep | Coverage Camera | 可空 | 优先 Inspector 引用，否则出场时读取 MainCamera |
| LongWolfSweep | Body Width | 0.8 世界单位 | 至少 0.1；同时决定身体、预警和扫掠宽度，头部同比收细 |
| Wolf | Warning Flash Frequency | 5 次/秒 | 非负；长狼红色预警按亮/灭各半周期闪烁，0 为常亮 |
| Wolf | Warning Duration | 1.2 秒 | 非负 |
| Wolf | Charge Speed | 14 | 非负 |
| Wolf | Aim Follows Flock During Warning | false | 预警出现即锁定路线 |

生成与休息参数仍使用场景中的 WolfSpawner / WolfEventDirector，场景限制同时一只狼。
身体扫掠使用每只羊的 CircleCollider2D 中心及缩放后的半径，并覆盖两个物理帧之间的路段。
普通 Wolf 不挂 LongWolfSweep 时继续使用原来的撞散/叼一只逻辑。

自适应长度 = 镜头矩形沿冲锋方向的投影跨度 + 冲锋速度 × 覆盖时长。
覆盖指狼身同时跨越镜头投影两端，不是整只狼的总在屏时间，也不是涂满屏幕；斜向采用保守投影跨度。
覆盖时长在每次出场时于 0.8～1.2 秒之间均匀随机，不与羊数挂钩；进度通过镜头尺寸影响长度，镜头越大，狼越长。
以上新增配置由策划在 Inspector 编辑，LongWolfSweep 读取；本次随机时长和实际长度为实例运行时状态，不回写 Prefab。
长度在冲锋开始时锁定，后续镜头移动/缩放不实时重算，因此时长以锁定镜头为基准。宽度保持 0.8。
长狼在逃离时保持冲锋速度，且寿命按清屏路程延长，避免尾巴仍在屏幕上就消失。
长狼预警线覆盖镜头沿攻击方向的完整投影，两端额外留 2 世界单位；预警期间随镜头刷新长度，不因此改变锁定方向、闪烁频率或预警时长。

## 编辑器入口与验证

- `Game Jam > Wolf Test > Create TestLongWolf Scene`：首次创建；已存在时只打开，不覆盖调参。
- `Game Jam > Wolf Test > Open TestLongWolf Scene`：单独打开场景，再按 Play。
- `Game Jam > Wolf Test > Validate TestLongWolf`：在已保存的场景状态下运行专用验证。

验证结果输出到 `Tools/LongWolf-validation.txt`，画面到 `Tools/LongWolf-preview.png`。
验证使用临时运行时摆位检查高速多目标命中、路线外安全、暂停、全灭、重开，并重载后运行自然节奏。
测试摆位和速度覆盖不保存回场景或 Prefab；验证结束停在 TestLongWolf 编辑状态。
