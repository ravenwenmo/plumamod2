# 龙舌兰（Brother）多人模式位置刷新与半透明调研

日期：2026-08-21　任务：让 Brother 在多人模式下的刷新位置和半透明状态参考 Osty 实现

## 一、游戏源码中 Osty 的多人模式机制（0.111.0）

### 位置刷新（所有路径都收敛到同一套规则）

1. `NCombatRoom.PositionPlayersAndPets`（`src/Core/Nodes/Rooms/NCombatRoom.cs:370`）
   - 本地玩家（`LocalContext.IsMe`）的 Osty 被从通用宠物列表取出，经 `PositionLocalPlayerOsty`
     放在 `owner.Position + NCreature.GetOstyOffsetFromPlayer(...)`（偏移基于**主人 hitbox 尺寸**）。
   - **远程玩家**的 Osty 留在通用宠物列表：排在主人脚边、`ToggleIsInteractable(false)`、
     后排（num11 > 0）时 `Visuals.Modulate = new Color(0.5f,0.5f,0.5f)` 压暗、
     非本地宠物 `Visuals.Bounds.Visible = false`。
2. `NCombatRoom.AddCreature`（:534）
   - 本地 Osty：`OstyScaleToSize(maxHp, 0)` **即时**定位+缩放。
   - 其余宠物（含远程 Osty）：通用排布 + `ToggleIsInteractable(false)`；主人 Y < 199（后排）时压暗。
3. `NCreature.OstyScaleToSize`（:776）——**位置刷新核心**：
   - 位置 tween 只在 `LocalContext.IsMe(Entity.PetOwner)` 时执行；
   - 每次 Osty 血量/体型变化都会调用（召唤后、OstyCmd 结尾、死亡缩小），位置随之刷新。
4. 死亡：`NCreature` 死亡处理里 Osty 不销毁节点，而是 `OstyScaleToSize(0f, 0.75)` 缩小待复活。

### 半透明机制

- 后排玩家及其宠物整体压暗 `Visuals.Modulate = 0.5 灰`（`PositionPlayersAndPets:465-469`、
  `AddCreature:578`）——对所有宠物泛化，包括远程 Osty。
- 卡牌召唤淡入（`OstyCmd.Summon`）：`ostyNode.Modulate = Colors.Transparent`
  → tween 到 White（0.35s，延迟 0.1s）。用的是**节点级** modulate，
  与 Visuals 级的后排压暗相乘不冲突；另调 `StartReviveAnim()`（Osty 的 spine 有 revive 动画）。
- 远程宠物的血条 `_stateDisplay.HideImmediately()`、Bounds 高亮框隐藏——游戏在 `NCreature` 初始化时统一处理。

## 二、Brother 现状与 Osty 的不一致点

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| 1 | 定位 tween 与 `Hitbox.MouseFilter = Stop` 在**所有客户端**执行，无 `IsMe` 门控 | `BrotherCmd.Summon` | 远程客户端上龙舌兰偏离游戏通用宠物布局、hitbox 拦截鼠标（遮挡出牌瞄准/点选） |
| 2 | `ToggleIsInteractable(true)` 无条件执行 | `Brother.AfterAddedToRoom` | 远程客户端上强制显示血条（游戏对远程宠物是隐藏的）、可交互——违反远程宠物规则 |
| 3 | 战斗开始时自动召唤若节点未就绪，tween 被跳过且**之后不再刷新位置**（无 OstyScaleToSize 等价物） | `BrotherCmd.Summon` | 多人模式时序差异下各客户端位置不一致/停在通用宠物位 |
| 4 | 无卡牌召唤淡入（Osty 有 Transparent→White 淡入） | —— | 召唤瞬间弹出 |
| 5 | `BrotherCmd.Summon` 手动调 `AfterAddedToRoom()`，而游戏在 `CreatureCmd.Add`→`CombatManager.AfterCreatureAdded` 里**已经调用过一次**（0.111.0 新增行为），导致力量/特性 Power 叠加两次、动画触发两次 | `BrotherCmd.Summon:57` | 与 OstyCmd 不一致；若修复会改变力量/特性数值（影响平衡），需用户确认 |

附注：
- 单人模式 `LocalContext.NetId` 由 `RunManager:713` 设置（单机为 fake net），`IsMe(owner)` 为 true，
  因此加 `IsMe` 门控**不影响单人行为**（Osty 单人定位同样走该门控）。
- Brother spine（char_486_takila.json）**没有** revive 动画 → 不调 `StartReviveAnim`。

## 三、修改计划（用户已确认，2026-08-21 实施）

1. `Scripts/Commands/BrotherCmd.cs` ✅
   - 定位 tween 与 `Hitbox.MouseFilter = Stop` 增加 `LocalContext.IsMe(summoner)` 门控；
     定位公式保持现状（`当前布局位置 + GetBrotherOffsetFromPlayer`），避免影响单人。
   - `source is CardModel` 时增加节点级淡入（mirror OstyCmd，不调 StartReviveAnim）。
   - `GetBrotherOffsetFromPlayer` 改为 public static 供补丁复用。
2. `Scripts/Monsters/Brother/Brother.cs` ✅
   - `AfterAddedToRoom` 尾部 `ToggleIsInteractable(true)` 增加 `IsMe(Creature.PetOwner)` 门控。
3. 新增 `Scripts/Patch/BrotherLayoutPatch.cs` ✅
   - Harmony postfix `NCombatRoom.PositionPlayersAndPets`：对 Brother 且 `IsMe(PetOwner)` 的节点
     补上偏移位置 + `ToggleIsInteractable(true)`，覆盖"房间创建/布局晚于自动召唤"的时序缺口
     （对应游戏对本地 Osty 在同一方法里的特殊处理）。
4. 双调用问题：用户确认**保持现状**（本次未修改 `BrotherCmd.Summon:57` 的手动调用）。

构建验证：dotnet build 0 错误（243 个既有警告，与改动无关）。

## 五、待用户测试的验证点

1. **单人回归**：TestBrother 卡召唤——位置与之前一致、可交互、血条正常；召唤有淡入。
2. **多人 2P（羽毛笔 + 任意角色）**：
   - 本机视角：自己的龙舌兰在正确偏移位置、可选中（悬停有意图/血条）；队友的龙舌兰
     在队友脚边通用宠物位、无血条、鼠标可穿透（不遮挡出牌瞄准）。
   - 后排玩家（第 2 行）的龙舌兰应保持 50% 压暗。
   - 战斗开始时自动召唤的位置正确（两客户端各看各的本地龙舌兰偏移）。
   - 卡牌召唤龙舌兰有淡入效果。
3. **死亡后重召**：多人下龙舌兰死亡后用 TestBrother 重召，位置/淡入/可交互均正常。

## 四、多人安全说明

- 所有改动都是**视觉层、每客户端本地执行**（Godot Tween / 节点 Position / Modulate / 交互状态），
  不写入任何游戏状态 → 不参与同步、不破坏命令确定性。
- 不使用 `new Random()`；不触碰 dealer（`AfterDamageReceivedPatch` 已保证 Brother 伤害时 dealer 为 null）；
  瞄准预览、光效等本地 UI 状态不受影响。
- Harmony 补丁由 `Entry.Init` 的 `harmony.PatchAll()` 自动注册，无需改注册代码。
