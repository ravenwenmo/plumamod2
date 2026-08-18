using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;
using Godot;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;
using  STS2RitsuLib.Scaffolding.Cards.HandOutline;
using  STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards; // 提供 CardType 枚举
using HarmonyLib;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts;

    
[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "pluma";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 皮肤数据句柄（现在使用引用类型 SkinIndexWrapper）
    public static PlayerRunSavedData<SkinIndexWrapper> SkinData = null!;

    // 高速切割层数数据句柄（run 内跨战斗保留，战斗外存档/读档恢复）
    public static PlayerRunSavedData<RapidSlashingStacksSave> RapidSlashingStacksData = null!;

    // 龙舌兰状态存储
    public static PlayerRunSavedData<TequilaState> TequilaStateData = null!;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 注册自定义目标类型"任意单位"（基酒/鸡尾酒牌使用），须在内容注册前完成
        _ = PlumaTargetTypes.AnyUnit;

        PlumaSettingsPage.Register();

        // 注册先古升级和遗物升级
        RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<RapidSlashing, RapidSlashingMax>();
        RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<ReaperBadge, AquaDawn>();

        // 注册皮肤数据槽位
        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            var store = RitsuLibFramework.GetRunSavedDataStore(ModId);
            SkinData = store.RegisterPerPlayer(
                key: "skin_index",
                // 默认必须是确定性的 0，不能取 LocalIndex：
                // 多人模式下某玩家从未在大厅选过皮肤时，各台机器都会用各自的
                // defaultFactory 兜底，若取本机配置会导致每台机器渲染结果不一致。
                // 本机玩家的皮肤由 RunSavedDataPreparingEvent 处理器写入槽位。
                defaultFactory: () => new SkinIndexWrapper(),
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true
                });

            RapidSlashingStacksData = store.RegisterPerPlayer(
                key: "rapid_slashing_stacks",
                // 默认 0 层：新 run 槽位为空，不跨 run 继承层数
                defaultFactory: () => new RapidSlashingStacksSave(),
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet
                });

            TequilaStateData = store.RegisterPerPlayer(
                key: "tequila_state",
                defaultFactory: () => new TequilaState(),
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet
                });
        }

        var harmony = new Harmony("com.lapluma.patch"); // patch的ID，和别人写的不一致防撞车
        harmony.PatchAll();

        // 注册皮肤同步所需的补丁：追踪当前大厅、为战斗模型创建提供玩家上下文
        var skinPatcher = RitsuLibFramework.CreatePatcher(ModId, "skin-sync");
        skinPatcher.RegisterPatch<PlumaStartRunLobbyCtorPatch>();
        skinPatcher.RegisterPatch<PlumaStartRunLobbyCleanUpPatch>();
        skinPatcher.RegisterPatch<PlumaCreatureVisualsContextPatch>();
        skinPatcher.RegisterPatch<PlumaRunInitSkinPatch>();
        RitsuLibFramework.ApplyRequiredPatcher(
            skinPatcher,
            static () => { }, // 补丁失败不会禁用整个 mod，仅记录日志
            "pluma 皮肤同步补丁应用失败");
        GD.Print($"[pluma] skin-sync patcher: applied={skinPatcher.AppliedPatchCount}/{skinPatcher.RegisteredPatchCount}, isApplied={skinPatcher.IsApplied}");

        // 开局初始化时（RunSavedData 已导入/准备好之后）确保本地玩家在槽位中有皮肤值。
        // 多人模式下大厅暂存值会通过 payload 同步过来，这里只兜底写入本地玩家自己的值；
        // 单人模式下槽位为空，这里写入本地配置作为局内读取来源。
        // 注意：若多人模式下该事件未触发，PlumaRunInitSkinPatch 会在 InitializeNewRun 后
        // 执行相同的幂等逻辑（见 PlumaSkinSyncAction.EnsureLocalSkinSynced）。
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(evt =>
        {
            GD.Print($"[pluma] RunSavedDataPreparing event: multiplayer={evt.IsMultiplayer}");
            PlumaSkinSyncAction.EnsureLocalSkinSynced(evt.RunState);
        });


        // 自动为所有攻击牌注册固定金色发光（条件：拥有切割关键词且连击数 > 0）
        var slashingAssembly = Assembly.GetExecutingAssembly();
        var attackCardTypes = slashingAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ModCardTemplate).IsAssignableFrom(t))
            .Where(t =>
            {
                try
                {
                    var instance = (ModCardTemplate)Activator.CreateInstance(t);
                    return instance.Type == CardType.Attack;
                }
                catch { return false; }
            });

        foreach (var cardType in attackCardTypes)
        {
            ModCardHandOutlineRegistry.Register(
                cardType,
                ModCardHandOutlineRules.Dynamic(
                    card => card.Keywords.Contains(MyKeywords.Slashing) &&
                            SlashingComboSingleton.GetPlayerComboCount(card.Owner) > 0,
                    card =>
                    {
                        int combo = SlashingComboSingleton.GetPlayerComboCount(card.Owner);
                        float factor = Math.Clamp((combo - 1) / 4f, 0f, 1f);
                        return new Color(1f, 0.843f * (1f - factor), 0f);
                    },
                    priority: 5
                )
            );
        }

        // 三形态卡牌（基酒/鸡尾酒）按当前 SpiritMode 显示独立描述
        SpiritModeDescriptionPatch.Apply();

        // 追踪卡牌选目标过程：兜割瞄准时显示创伤血条预测，
        // 基酒/鸡尾酒瞄准时按目标阵营切换卡牌描述
        CardAimPreviewPatch.Apply();

    }
}

/// <summary>
/// 包装皮肤索引的引用类型（满足 RegisterPerPlayer 的 class 约束）
/// </summary>
public class SkinIndexWrapper
{
    public int Index { get; set; }
}