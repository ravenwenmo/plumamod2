using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.RunData;
using Godot;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;
using  STS2RitsuLib.Scaffolding.Cards.HandOutline;
using  STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards; // 提供 CardType 枚举

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
                defaultFactory: () => new SkinIndexWrapper { Index = PlumaSkins.LocalIndex },
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true
                });
        }


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