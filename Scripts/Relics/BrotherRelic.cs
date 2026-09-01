using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
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
using Godot;

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
        new DynamicVar("Turns", BrotherStateData.SavedAttackTurnsRemaining[this])
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

    public override async Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player != Owner || isMimicked) return;
        
        await HealBrotherOption.ExecuteRestSiteHeal(player);
    }

    private void UpdateDisplay()
    {
        DynamicVars["HP"].BaseValue = BrotherStateData.SavedHp[this];
        DynamicVars["MaxHP"].BaseValue = BrotherStateData.SavedMaxHp[this];
        DynamicVars["Trait"].BaseValue = BrotherStateData.SavedTrait[this];
        DynamicVars["Turns"].BaseValue = BrotherStateData.SavedAttackTurnsRemaining[this];
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
                decimal amount = BrotherStateData.SavedMaxHp[relic] - BrotherStateData.SavedHp[relic];
                if (RunManager.Instance.HasAscension(AscensionLevel.WearyTraveler))
                {
                    amount *= 0.8m;
                }
                BrotherStateData.Heal(__instance.Owner, (int)amount);
                GD.Print($"[BrotherRelicAncientEventPatch] Healed Brother for {amount} HP. Current HP: {BrotherStateData.SavedHp[relic]}");
            }
        }
    }
}