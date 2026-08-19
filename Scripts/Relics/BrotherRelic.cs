using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Pluma.Scripts.Commands;
using Pluma.Scripts.Monsters;
using Pluma.Scripts.Option;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace Pluma.Scripts;

[RegisterRelic(typeof(PlumaRelicPool))]
public class BrotherRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool SpawnsPets => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("HP", BrotherStateData.SavedHp[this]),
        new DynamicVar("MaxHP", BrotherStateData.SavedMaxHp[this]),
        new DynamicVar("Strength", BrotherStateData.SavedStrength[this]),
        new DynamicVar("Turns", BrotherStateData.SavedAttackTurnsRemaining[this])
    ];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        DynamicVars["HP"].BaseValue = BrotherStateData.SavedHp[this];
        DynamicVars["MaxHP"].BaseValue = BrotherStateData.SavedMaxHp[this];
        DynamicVars["Strength"].BaseValue = BrotherStateData.SavedStrength[this];
        DynamicVars["Turns"].BaseValue = BrotherStateData.SavedAttackTurnsRemaining[this];
    }

    public override async Task BeforeCombatStart()
    {
        await BrotherCmd.AutoSummon(Owner);
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

    public static void UpdateSavedHP(Player player, int hp)
    {
        BrotherStateData.SavedHp[player.GetRelic<BrotherRelic>()] = hp;
        player.GetRelic<BrotherRelic>().DynamicVars["HP"].BaseValue = hp;
    }

    public static void UpdateSavedMaxHP(Player player, int maxHp)
    {
        BrotherStateData.SavedMaxHp[player.GetRelic<BrotherRelic>()] = maxHp;
        player.GetRelic<BrotherRelic>().DynamicVars["MaxHP"].BaseValue = maxHp;
    }

    public static void UpdateSavedStrength(Player player, int strength)
    {
        BrotherStateData.SavedStrength[player.GetRelic<BrotherRelic>()] = strength;
        player.GetRelic<BrotherRelic>().DynamicVars["Strength"].BaseValue = strength;
    }

    public static void UpdateSavedAttackTurnsRemaining(Player player, int turns)
    {
        BrotherStateData.SavedAttackTurnsRemaining[player.GetRelic<BrotherRelic>()] = turns;
        player.GetRelic<BrotherRelic>().DynamicVars["Turns"].BaseValue = turns;
    }
}