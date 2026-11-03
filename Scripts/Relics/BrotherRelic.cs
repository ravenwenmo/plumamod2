using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Commands;
using Pluma.Scripts.Monsters;
using Pluma.Scripts.Option;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using MegaCrit.Sts2.Core.Models;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Ascension;

namespace Pluma.Scripts;

[RegisterRelic(typeof(PlumaRelicPool))]
public class BrotherRelic : ModRelicTemplate, IRelicExtraIconAmountLabelSpecsProvider
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool SpawnsPets => true;

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        int hp = BrotherStateData.SavedHp[this];
        string hpText = "[font_size=24]" + hp.ToString() + "[/font_size]";
        int maxHp = BrotherStateData.SavedMaxHp[this];
        if (hp > maxHp / 2)
        {
            hpText = "[green]" + hpText + "[/green]";
        }
        else if (hp > maxHp / 4)
        {
            hpText = "[gold]" + hpText + "[/gold]";
        }
        else
        {
            hpText = "[red]" + hpText + "[/red]";
        }

        int trait = BrotherStateData.SavedTrait[this];
        string traitText = "[font_size=24]" + trait.ToString() + "[/font_size]";

        return
        [
            ExtraIconAmountLabelSpec.RichTextCustom(traitText,
                50, 0, 82, 32
            ),
            ExtraIconAmountLabelSpec.RichTextCustom(hpText,
                50, 50, 82, 82
            ),
        ];
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("HP", BrotherStateData.SavedHp[this]),
        new DynamicVar("MaxHP", BrotherStateData.SavedMaxHp[this]),
        new DynamicVar("Trait", BrotherStateData.SavedTrait[this]),
        new DynamicVar("Turns", BrotherStateData.SavedAttackTurnsRemaining[this]),
        // 基础攻击力：构造时只能给白值，真正的数值由 UpdateDisplay 走伤害管线实时算出来
        new DynamicVar("BasicDamage", Brother.BasicDamage)
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    private bool _hasSummonedBrother = false;    // 实例字段，每人一个

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner.GetRelic<BrotherRelic>() == null) return;
        UpdateDisplay();
        _hasSummonedBrother = false;
    }

    public override async Task BeforeCombatStart()
    {
        if (_hasSummonedBrother) return;
        await BrotherCmd.AutoSummon(Owner);
        _hasSummonedBrother = true;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
        {
            return false;
        }

        options.Add(new HealBrotherOption(player));
        return true;
    }

    // 龙舌兰的“休息”回血（60% 最大生命）唯一结算点：
    // 羽毛笔的休息选项 HealBrotherOption.OnSelect 会广播 Hook.AfterRestSiteHeal，
    // 被队友“愈合”（MendRestSiteOption）以及原版模仿休息（MimicRestSiteHeal）也会走到这里。
    // 不要再去 HealBrotherOption.OnSelect 里直接调用 ExecuteRestSiteHeal，那会变成回两次血。
    public override async Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player != Owner || isMimicked) return;
        
        await HealBrotherOption.ExecuteRestSiteHeal(player);
    }

    // 龙舌兰的“基础攻击力”：直接把它结算伤害时用的那条管线跑一遍（Hook.ModifyDamage），
    // 与攻击意图 BrotherAttackIntent.GetDamage 用的是同一套调用，所以力量、活力、特性
    // 等一切“结算时才生效”的加成都会自动算进来，这里不需要逐个手写加成来源。
    // target 传 null：只想显示单体白值，不带易伤等目标侧修正。
    private int CalculateBasicDamage()
    {
        int baseDamage = Brother.BasicDamage;

        Player? owner = Owner;
        Creature? brother = owner?.Brother();
        ICombatState? combatState = owner?.Creature?.CombatState;

        // 非战斗（商店 / 休息处 / 结算界面）或龙舌兰不在场时没有战斗上下文，
        // 退回用持久化的特性层数按同一公式估算：特性每层 +1%（见 TraitPower.ModifyDamageMultiplicative），
        // 这样成长过的数值在战斗外也看得到，而不是永远显示 4。
        if (owner?.RunState == null || combatState == null || brother == null || !brother.IsAlive)
        {
            decimal traitScaled = baseDamage * (1m + BrotherStateData.SavedTrait[this] / 100m);
            return Math.Max(0, (int)traitScaled);
        }

        decimal num = Hook.ModifyDamage(
            owner.RunState,
            combatState,
            null,
            brother,
            baseDamage,
            ValueProp.Move,
            null,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out IEnumerable<AbstractModel> _
        );

        return Math.Max(0, (int)num);
    }

    // 龙舌兰身上的能力层数变化（特性、力量、活力……）都会改变它的基础攻击力，就地刷新一次描述。
    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (Owner?.Brother() == power.Owner)
        {
            UpdateDisplay();
        }

        return Task.CompletedTask;
    }

    private void UpdateDisplay()
    {
        DynamicVars["HP"].BaseValue = BrotherStateData.SavedHp[this];
        DynamicVars["MaxHP"].BaseValue = BrotherStateData.SavedMaxHp[this];
        DynamicVars["Trait"].BaseValue = BrotherStateData.SavedTrait[this];
        DynamicVars["Turns"].BaseValue = BrotherStateData.SavedAttackTurnsRemaining[this];
        DynamicVars["BasicDamage"].BaseValue = CalculateBasicDamage();
        InvokeDisplayAmountChanged();
    }

    public static void UpdateSavedHP(Player player, int hp)
    {
        BrotherStateData.SavedHp[player.GetRelic<BrotherRelic>()] = hp;
        player.GetRelic<BrotherRelic>().UpdateDisplay();
    }

    public static void UpdateSavedMaxHP(Player player, int maxHp)
    {
        BrotherStateData.SavedMaxHp[player.GetRelic<BrotherRelic>()] = maxHp;
        player.GetRelic<BrotherRelic>().UpdateDisplay();
    }

    public static void UpdateSavedTrait(Player player, int trait)
    {
        BrotherStateData.SavedTrait[player.GetRelic<BrotherRelic>()] = trait;
        player.GetRelic<BrotherRelic>().UpdateDisplay();
    }

    public static void UpdateSavedAttackTurnsRemaining(Player player, int turns)
    {
        BrotherStateData.SavedAttackTurnsRemaining[player.GetRelic<BrotherRelic>()] = turns;
        player.GetRelic<BrotherRelic>().UpdateDisplay();
    }
}

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
public static class BrotherRelicAncientEventPatch
{
    public static void Prefix(AncientEventModel __instance, bool isPreFinished)
    {
        if (!isPreFinished)
        {
            BrotherRelic relic = __instance.Owner.GetRelic<BrotherRelic>();
            if (relic != null)
            {
                // 多人同步修复：回血量必须从权威持久化状态 BrotherStateData 计算。
                // 不能读 relic.DynamicVars["HP"]——那是仅供 UI 展示的缓存值，主机与客机
                // 可能不一致；古代事件入场回血在两端算出不同的治疗量（如 6 vs 31），
                // 导致两个玩家的 BrotherRelic_SavedHp 在 checksum 中错位（7 vs 32），
                // 触发 StateDivergence 被强制踢出房间。
                decimal amount = BrotherStateData.GetMaxHp(__instance.Owner) - BrotherStateData.GetHp(__instance.Owner);
                if (RunManager.Instance.HasAscension(AscensionLevel.WearyTraveler))
                {
                    amount *= 0.8m;
                }
                BrotherStateData.Heal(__instance.Owner, (int)amount);
            }
        }
    }
}