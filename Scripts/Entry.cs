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
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Pluma.Scripts;

    
[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "pluma";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 皮肤数据句柄（现在使用引用类型 SkinIndexWrapper）
    public static PlayerRunSavedData<SkinIndexWrapper> SkinData = null!;

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
        }

        // 注册皮肤同步所需的补丁：追踪当前大厅、为战斗模型创建提供玩家上下文
        var skinPatcher = RitsuLibFramework.CreatePatcher(ModId, "skin-sync");
        skinPatcher.RegisterPatch<PlumaStartRunLobbyCtorPatch>();
        skinPatcher.RegisterPatch<PlumaStartRunLobbyCleanUpPatch>();
        skinPatcher.RegisterPatch<PlumaCreatureVisualsContextPatch>();
        RitsuLibFramework.ApplyRequiredPatcher(
            skinPatcher,
            static () => { }, // 补丁失败不会禁用整个 mod，仅记录日志
            "pluma 皮肤同步补丁应用失败");
        GD.Print($"[pluma] skin-sync patcher: applied={skinPatcher.AppliedPatchCount}/{skinPatcher.RegisteredPatchCount}, isApplied={skinPatcher.IsApplied}");

        // 开局初始化时（RunSavedData 已导入/准备好之后）确保本地玩家在槽位中有皮肤值。
        // 多人模式下大厅暂存值会通过 payload 同步过来，这里只兜底写入本地玩家自己的值；
        // 单人模式下槽位为空，这里写入本地配置作为局内读取来源。
        // 随后通过托管网络动作把本地玩家的皮肤广播到其他端（大厅暂存链路之外的兜底）。
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataPreparingEvent>(evt =>
        {
            try
            {
                GD.Print($"[pluma] RunSavedDataPreparing event: multiplayer={evt.IsMultiplayer}");

                Player? me = null;
                try
                {
                    me = LocalContext.GetMe(evt.RunState);
                }
                catch
                {
                    // NetId 已设置但集合中找不到本地玩家等异常，走回退逻辑
                }
                me ??= evt.RunState.Players.FirstOrDefault(p => p.Character is PlumaCharacter);

                if (me == null)
                {
                    GD.Print("[pluma] RunSavedDataPreparing: 未找到本地玩家，跳过皮肤处理");
                    return;
                }

                if (!SkinData.TryGet(evt.RunState, me.NetId, out _))
                {
                    SkinData.Modify(me, wrapper => wrapper.Index = PlumaSkins.LocalIndex);
                    GD.Print($"[pluma] RunSavedDataPreparing: 为本地玩家 {me.NetId} 写入皮肤 {PlumaSkins.LocalIndex}");
                }

                // 广播一次本地玩家的皮肤（所有端执行时写入该玩家的槽位）
                PlumaSkinSyncAction.TrySyncLocalSkin(PlumaSkins.LocalIndex);
            }
            catch (Exception ex)
            {
                Logger.Warn("[pluma] RunSavedDataPreparing 皮肤兜底写入失败: " + ex.Message);
            }
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

    }
}

/// <summary>
/// 包装皮肤索引的引用类型（满足 RegisterPerPlayer 的 class 约束）
/// </summary>
public class SkinIndexWrapper
{
    public int Index { get; set; }
}