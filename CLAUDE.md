# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **Slay the Spire 2 (STS2) mod** named "pluma" (羽毛笔), adding a fan character with a scythe (Liberi/Pluma), a ~60-card pool, powers, relics, and skins. STS2 itself is built on Godot 4.5, so STS2 mods are Godot C# projects: this repo is opened in the Godot editor for scenes/resources, while all gameplay code is C# referencing the game's own `sts2.dll` and the **STS2.RitsuLib** modding framework.

The codebase is commented in Chinese and the developer works in Chinese; mechanics have both English class names and Chinese display names (切割=Slashing, 渐入佳境=Flow, 创伤=OpenWound, 本能=MuscleMemory, 基酒=BaseSpirit, 黎博利=Liberi).

## Build & deploy

```bash
dotnet build          # builds pluma.dll; post-build target auto-copies pluma.dll + pluma.json
                      # to $(Sts2Dir)\mods\pluma\ and the RitsuLib NuGet to mods\STS2-RitsuLib\
```

- `pluma.csproj` hardcodes `<Sts2Dir>E:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Dir>` — change it if the game install moves. Targets net9.0, LangVersion 13, references `sts2.dll` and `0Harmony.dll` from `$(Sts2Dir)\data_sts2_windows_x86_64\`.
- **The DLL is only half the mod.** Gameplay assets (card/relic/power art, spine scenes, localization JSON) live under `pluma/` and must be exported as `pluma.pck` from the Godot editor using the "pluma" export preset, then placed in `mods\pluma\` (the MSBuild target does NOT copy the pck). `pluma.json` declares `has_pck: true`.
- Godot editor path: `d:\Godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe` (see `.vscode/settings.json`). `bin/` contains the spine_godot GDExtension so the editor can open the game's spine `.tscn` scenes.
- There are no tests. Playtesting = launch STS2 and start a run with the character.
- **CombatSolver 适配默认不编入**：`pluma.csproj` 的 `PlumaCombatSolver` 开关默认为 `false`。编入（`dotnet build -p:PlumaCombatSolver=true`，或把 csproj 里的 false 改成 true）后 pluma.dll 会硬引用 `mods\CombatSolver\CombatSolver.dll`；此时**游戏里必须启用 CombatSolver**，否则游戏加载 Mod 枚举类型时抛 `ReflectionTypeLoadException`，pluma 完全加载不了（mods 目录里有 CombatSolver.dll 但游戏设置里禁用了它，就会踩到）。默认关闭时 pluma 在 CombatSolver 开或关时都能加载，只是没有镜像。每次构建都会把当前状态打在构建日志里。
- `pluma.json`: mod metadata + dependency on `STS2-RitsuLib` (min 0.6.11 — 0.113 对应的 RitsuLib 版本); `min_game_version` 0.113.0. Bump version there for releases.
- `print_test_card_images.py`: generates blank 250x190 placeholder PNGs for every card class that lacks art.

## Architecture

Everything registers through **RitsuLib auto-registration**: the game loads `pluma.dll`, finds `Scripts/Entry.cs` via `[ModInitializer(nameof(Init))]`, and `Entry.Init()` registers the assembly with `ModTypeDiscoveryHub`. Content classes then self-register via attributes — no manual registration lists.

### Content templates (the core pattern)

One class per card/power/relic, each extending a RitsuLib template and carrying a `[Register*]` attribute:

- **Cards** — `Scripts/Cards/`: `[RegisterCard(typeof(PlumaCardPool))]` for pool cards, `[RegisterCard(typeof(TokenCardPool))]` for generated tokens (e.g. `Slashing`, `Gin`). Extend `ModCardTemplate`; declare cost/type/rarity/target as consts, `CanonicalKeywords`, `CanonicalVars` (DynamicVars for damage/block), `CanonicalTags` (custom tags from `PlumaTags`), and `override async Task OnPlay(PlayerChoiceContext, CardPlay)`. All game actions go through the command pattern, e.g. `DamageCmd.Attack(v).FromCard(this, cardPlay).Targeting(...).Execute(choiceContext)`, `PowerCmd.Apply<T>(...)`, `CardPileCmd.*` — never mutate state directly.
- **Powers** — `Scripts/Powers/`: `[RegisterPower]`, extend `ModPowerTemplate`; logic lives in hook overrides (`ModifyDamageMultiplicative`, `AfterPowerAmountChanged`, `AfterSideTurnEnd`, etc.).
- **Relics** — `Scripts/Relics/`: `[RegisterRelic(typeof(PlumaRelicPool))]`; starter relic additionally gets `[RegisterCharacterStarterRelic(typeof(PlumaCharacter))]`.
- **Character** — `Scripts/PlumaCharacter.cs`: `ModCharacterTemplate<PlumaCardPool, PlumaRelicPool, PlumaPotionPool>`; `AssetProfile` merges `CharacterAssetProfiles.Ironclad()` with mod-specific paths.
- **Card pool** — `Scripts/PlumaCardPool.cs`: `TypeListCardPoolModel`; card frame tinting via `MaterialUtils.CreateReplaceHueShaderMaterial`.
- **Keywords/tags** — `Scripts/Keywords/MyKeywords.cs` and `Scripts/PlumaTags.cs`: `[RegisterOwnedCardKeyword(nameof(X))]` / `[RegisterOwnedCardTag(...)]` attributes on the class; static fields via `ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(X)).GetModCardKeyword()`. Note the comment: these must NOT be static classes.

### Cross-cutting systems

- **`Scripts/SlashingComboSingleton.cs`** — `[RegisterSingleton]` + `HookedSingletonModel(HookType.Combat)`; maintains per-player combo counters (keyed on `Player`, cleared on combat room enter AND combat end — a double clear, per an in-code comment about a past bug). Hooks `AfterCardPlayed` (切割 cards increment, others reset unless `PrecisionRepetitionPower` is active) and `AfterDamageGiven` (applies `OpenWoundPower` stacks). Exposed statically via `Instance` for use outside hooks.
- **`Scripts/Entry.cs`** — besides registration, wires: relic transcendence mappings (`RapidSlashing` → `RapidSlashingMax` via Archaic Tooth, `ReaperBadge` → `AquaDawn` via Touch of Orobas), a per-player run-saved-data slot (`skin_index`, multiplayer-synced), and a dynamic hand-outline glow registered for every attack card (orange glow scaling with combo count when the card has the Slashing keyword).
- **Skins** — `Scripts/Skins/`: `PlumaSkins` is a static registry of `SkinData` (visuals/spine scene, select-screen bg, icons); index persists locally to `user://pluma_skin.cfg` and syncs in multiplayer via the run-data slot. `PlumaCharacter.TryCreateCreatureVisuals()` overrides the static profile path to always use the current skin. `SkinSelector` is a Godot UI `Control` for the settings page; `PlumaSettingsPage` registers a RitsuLib settings page ("羽毛笔") with `ModSettingsValueBinding`s.
- **Spirit mode** — `Gin` (基酒) implements `IModRightClickableCard` and cycles Self→Enemy→Ally on right-click, changing `TargetType` and glow dynamically. `Scripts/ISpiritCard.cs` is an abandoned draft of this (`<Compile Remove>` in the csproj); `Scripts/SpiritMode.cs` holds the enum.
- **Cocktail** applies 3 random buffs from a manually maintained `BuffAppliers` list of `PowerCmd.Apply<TPower>` lambdas — when adding a new buff power, it must be added to this list explicitly.
- **Flow (渐入佳境) engine** — `FlowState` draws attack/本能 (MuscleMemory) cards from the draw pile, temporarily discounts them 1 energy, auto-plays them if `TransparentWorldPower` is present (with a static `AutoSkipCounterSelect` flag for `CounterIntuitive`), and grants +3% attack damage per stack. Related powers (WindFeather, ConstantFlow, LikeABird, etc.) hook into or suppress this.
- **`Scripts/Patch/PlumaAttackAnimSpeedPatch.cs`** — Harmony postfix on `NCreature.SetAnimationTrigger`：羽毛笔默认攻击动画（"Attack" 触发器 → "attack" 状态）按渐入佳境层数加速，速度 = 1 + 3%×min(层数, 12)。速度写在当前 TrackEntry 上（后续动画是新条目、默认 1.0，无需恢复）；`[HarmonyPatch]` 属性类由 `Entry.cs` 的 `harmony.PatchAll()` 自动应用。

## Conventions

- **File name == class name**, and resources are resolved by path convention from it: card portraits at `res://pluma/images/cards/{ClassName}.png`, power icons at `res://pluma/images/powers/{ClassName}.png`.
- **Localization**: `pluma/localization/zhs/*.json` — the game auto-binds by key convention `PLUMA_CARD_<CLASSNAME>`, `PLUMA_POWER_<CLASSNAME>`, `PLUMA_KEYWORD_<NAME>`, `PLUMA_RELIC_<CLASSNAME>`, `PLUMA_CHARACTER_<CLASSNAME>` with `.title` / `.description` / `.smartDescription` fields. Descriptions use DynamicVar placeholders like `{Damage:diff()}` and BBCode color tags (`[gold]`, `[blue]`, `[sine]`). New content needs a matching localization entry or it will show raw keys.
- Godot scene/resource paths inside code are `res://pluma/...` strings — these resolve against the exported `pluma.pck`, so resources must exist under `pluma/` in the Godot project or they 404 at runtime.
- `Scripts/Others/` and `Scripts/Test/` are empty placeholder directories referenced by the csproj.
- `.godot/`, `bin/`, `.idea/` are editor-generated; `pluma.pck` is a build artifact (untracked).
- STS2 is multiplayer-capable: state that belongs to a player must be per-`Player` (see combo counters), and actions must be deterministic command-based so they replay/sync correctly.

## 语言偏好

请始终使用简体中文回答问题、解释代码和给出建议。

## 外部参考源码

原游戏源码位于 `D:\plumamod2\Slay the Spire 2 0.113`（当前对应版本）、`D:\plumamod2\Slay the Spire 2 0.111.0` 与 `D:\plumamod2\Slay the Spire 2 0.110.0`（旧版对照），仅用于查阅游戏 API、基类实现和框架逻辑。

- 只读取该目录，不要修改其中任何文件。
- 不要将源码复制到本 Mod 项目内。

## Git 规则

- 修改代码后，不要自动执行 git commit 或 git push。
- 如果需要提交，先运行 git status 和 git diff 展示改动，等用户确认。
- 构建产物如 pluma.pck、.godot/、bin/ 不要提交，依赖 .gitignore 管理。