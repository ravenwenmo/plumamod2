# AGENTS.md — pluma 速查（新对话先看这份）

杀戮尖塔 2（STS2，Godot 4.5 + C#）模组「羽毛笔 / pluma」：一名持镰刀的黎博利调酒师角色，含约 60 张卡牌、能力、遗物、皮肤、一个同伴（龙舌兰）。
代码注释与交流一律用**简体中文**。详细架构、机制说明见 [CLAUDE.md](CLAUDE.md)（本文档只放"去哪儿找、怎么改、怎么编"）。

## 1. 查找顺序（重要）

**先搜 `Scripts/`，不要全仓库乱找。** 玩法逻辑全部在 `D:\plumamod2\pluma\Scripts`，`bin/`、`.godot/`、`pluma.pck`、`images/` 是产物或资源，`docs/` 是历史调查笔记。

| 要找的东西 | 路径 | 命名规律 |
| --- | --- | --- |
| 卡牌 | `Scripts/Cards/`（基酒 `Cards/BaseLiquor/`、特调 `Cards/Cocktails/`） | 文件名 = 类名，如 `FullSpeedSlash.cs` |
| 能力/Power | `Scripts/Powers/` | 同上，如 `FlowState.cs`（渐入佳境） |
| 遗物 | `Scripts/Relics/` | 同上，如 `GoldenPlains.cs`（黄金平原） |
| 怪物/同伴 | `Scripts/Monsters/` | `Brother/` 是龙舌兰 |
| Harmony 补丁 | `Scripts/Patch/` | 类上 `[HarmonyPatch]`，`Entry.cs` 里 `PatchAll()` |
| 注册入口 | `Scripts/Entry.cs` | `[ModInitializer(nameof(Init))]` |
| 卡池/角色/初始遗物 | `Scripts/PlumaCardPool.cs`、`PlumaCharacter.cs`、`PlumaRelicPool.cs`、`PlumaPotionPool.cs` | |
| 关键词/自定义标签 | `Scripts/Keywords/MyKeywords.cs`、`Scripts/PlumaTags.cs` | 必须是非 static 类 |
| CombatSolver 预测适配 | `Scripts/CombatSolverAdapter/` | 默认不编入（见 csproj 开关） |
| 皮肤/设置页 | `Scripts/Skins/` | |

- 内容类全靠 **RitsuLib 自动注册**：类上带 `[RegisterCard(typeof(PlumaCardPool))]` / `[RegisterPower]` / `[RegisterRelic(typeof(PlumaRelicPool))]`，不用手写注册列表。
- 令牌类卡（基酒等）用 `[RegisterCard(typeof(TokenCardPool))]`。
- **文件名必须等于类名**，美术资源路径按类名推导（见下）。

## 2. 本地化文本位置

`D:\plumamod2\pluma\pluma\localization\zhs\`（只有中文 `zhs`）：

| 文件 | 内容 |
| --- | --- |
| `cards.json` | 卡牌 |
| `powers.json` | 能力 |
| `relics.json` | 遗物 |
| `card_keywords.json` | 关键词 |
| `characters.json` / `ancients.json` / `events.json` / `monsters.json` / `intents.json` / `rest_site_ui.json` / `static_hover_tips.json` | 其余 |

键名约定（`<CLASSNAME>` 用**英文类名大写**），以及各文件实际用到的字段：

- 卡牌 `PLUMA_CARD_<CLASSNAME>.title / .description`（选择类卡另有 `.selectPrompt`）
- 能力 `PLUMA_POWER_<CLASSNAME>.title / .description / .smartDescription`（**能力基本都写 `smartDescription`**）
- 遗物 `PLUMA_RELIC_<CLASSNAME>.title / .description / .flavor`
- 关键词 `PLUMA_KEYWORD_<NAME>.title / .description`
- 角色 `PLUMA_CHARACTER_<CLASSNAME>.title / .description / .pronoun* / .unlockText` 等一套角色字段

漏写条目游戏里会直接显示原始 key（如 `PLUMA_CARD_XXX.title`）。

文本写法：

- 动态数值占位符 `{Damage:diff()}`、`{Hits:diff()}`、`{Energy:energyIcons()}`、`{Cards:diff()}`，必须与代码里 `CanonicalVars` 的变量名一致，否则显示原始占位符。
- `{InCombat:…|}` 只在战斗内显示，`{IfUpgraded:…|}` 只在已升级时显示。
- 颜色/特效 BBCode：`[gold]`、`[blue]`、`[red]`、`[green]`、`[sine]`、`[font_size=24]`。
- 换行用 `\n`。
- ⚠️ 这是 **pck 里的资源**：改完本地化要重新用 Godot 导出 `pluma.pck`（`dotnet build` 只拷 dll，不拷 pck）。

## 3. 美术与场景资源

- 卡图 `pluma/images/cards/{类名}.png`、能力图标 `pluma/images/powers/{类名}.png`、遗物图标 `pluma/images/relics/{类名}.png`（代码里写 `res://pluma/...`，按类名拼）。
- 缺卡图时用仓库根的 `print_test_card_images.py` 生成 250x190 占位图。
- **`res://` 路径解析到导出的 `pluma.pck`**，资源不在 `pluma/` 下就会运行时 404。

## 4. 构建 / 部署

```bash
dotnet build          # 编 pluma.dll，并自动复制 dll + pluma.json 到 $(Sts2Dir)\mods\pluma\
```

- `pluma.csproj` 里 `<Sts2Dir>E:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Dir>`，游戏换盘要改这里。
- 目标框架 net9.0，引用游戏目录下 `data_sts2_windows_x86_64\sts2.dll`、`0Harmony.dll`，以及 NuGet 的 `STS2.RitsuLib`（版本写死，勿用 `*`）。
- **CombatSolver 适配默认不编入**：`-p:PlumaCombatSolver=true` 才开；开了之后游戏里必须启用 CombatSolver，否则 pluma 整个加载失败（`ReflectionTypeLoadException`）。
- Godot 编辑器：`d:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe`，导出预设 "pluma" → `pluma.pck` → 放进 `mods\pluma\`。
- 没有单元测试，验证 = 进游戏打一局。
- 在 DSH 沙箱里 `dotnet build` 会被拒（要写 `.godot/mono/temp/obj` 和游戏 `mods` 目录），需要提权到完全权限跑这一条命令。

## 5. 外部参考源码（只读）

- `D:\plumamod2\Slay the Spire 2 0.113`（当前对应版本）、`D:\plumamod2\Slay the Spire 2 0.111.0` / `0.110.0`（旧版对照）。
- 查原版 API、基类、钩子签名、原版卡牌/遗物实现就翻这里：卡片 `src/Core/Models/Cards/`、能力 `src/Core/Models/Powers/`、遗物 `src/Core/Models/Relics/`、命令 `src/Core/Commands/`、钩子 `src/Core/Hooks/Hook.cs`、动态变量 `src/Core/Localization/DynamicVars/`。
- 只读，别改、别往本仓库拷。

## 6. 常用套路（写新内容时直接抄）

- 一切游戏动作走命令：`DamageCmd.Attack(...).WithHitCount(n)`、`PowerCmd.Apply<T>(ctx, target, ±amount, applier, cardSource)`（**负数就是扣层数**）、`CreatureCmd.GainBlock(...)`、`CardPileCmd.*`；不要直接改状态。
- 多段攻击用 `.WithHitCount(n)` 合并成一条攻击命令（活力等"每次攻击"效果才逐段生效）。
- 多人安全：属于玩家的状态要按 `Player` 存；逻辑要确定性、可回放。
- **卡面要显示"运行时算出来的数值"**（总段数、按层数变化的伤害等）：用 `CalculatedVar` + `CalculationBaseVar` + `CalculationExtraVar`，乘数由静态委托给：

```csharp
protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
{
    new DamageVar(4m, ValueProp.Move),
    new CalculationBaseVar(3),        // 基数
    new CalculationExtraVar(1m),      // 每个乘数单位 +1
    new CalculatedVar("TotalHits").WithMultiplier(StaticMultiplier) // 乘数 = 层数/2 之类
};
// 文本："造成{Damage:diff()}点伤害{TotalHits:diff()}次。"
// WithMultiplier 的委托必须是静态的（不能捕获模型实例），否则抛异常。
```

参考实现：`Scripts/Cards/FullSpeedSlash.cs`、`Scripts/Cards/ButFocused.cs`（计算伤害）、原版 `src/Core/Models/Cards/Barrage.cs`。

## 7. 协作约定

- 改代码后**不要自动 `git commit` / `git push`**；先给 `git status` / `git diff` 让用户确认。
- `pluma.pck`、`.godot/`、`bin/` 是产物，不要提交。
- 回答、注释、提交信息都用简体中文。
