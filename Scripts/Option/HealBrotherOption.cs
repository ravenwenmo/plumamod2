using Godot;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts.Option;

public class HealBrotherOption : RestSiteOption
{
    public override string OptionId => "Pluma_HealBrother";

    public override IEnumerable<string> AssetPaths
	{
		get
		{
			List<string> list = new List<string>();
			list.AddRange(base.AssetPaths);
			list.AddRange(NRestSmokeVfx.AssetPaths);
			list.AddRange(NDesaturateTransitionVfx.AssetPaths);
			return list;
		}
	}

	public override LocString Description
	{
		get
		{
			LocString description = base.Description;
			HealVar dynamicVar = new HealVar(GetBaseHealAmount(base.Owner))
			{
				PreviewValue = GetHealAmount(base.Owner)
			};
			description.Add("Character", base.Owner.Character.Id.Entry);
			description.Add(dynamicVar);
			IReadOnlyList<LocString> source = Hook.ModifyExtraRestSiteHealText(base.Owner.RunState, base.Owner, Array.Empty<LocString>());
			if (source.Any())
			{
				description.Add("ExtraText", "\n" + string.Join("\n", source.Select((LocString s) => s.GetFormattedText())));
			}
			else
			{
				description.Add("ExtraText", string.Empty);
			}
			return description;
		}
	}

    public HealBrotherOption(Player owner)
		: base(owner)
	{
	}

    public override async Task<bool> OnSelect()
    {
        await ExecuteRestSiteHeal(Owner);
		return true;
    }

    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default(CancellationToken))
	{
		PlayRestSiteHealSfx();
		NRestSiteRoom.Instance?.AddChildSafely(NRestSmokeVfx.Create());
		NRestSiteRoom.Instance?.AddChildSafely(NDesaturateTransitionVfx.Create());
		await Cmd.CustomScaledWait(1.5f, 2.5f, ignoreCombatEnd: false, ct);
	}

	public override Task DoRemotePostSelectVfx()
	{
		NDebugAudioManager.Instance?.Play("SOTE_SFX_SleepBlanket_v1.mp3", 0.5f, PitchVariance.Small);
		return Task.CompletedTask;
	}

    public static decimal GetHealAmount(Player player)
	{
		decimal healAmount = Hook.ModifyRestSiteHealAmount(player.RunState, player.Creature, GetBaseHealAmount(player));
		GD.Print($"Final heal amount for Brother: {healAmount}");
		return healAmount;
	}

    public static decimal GetBaseHealAmount(Player player)
	{
        int maxHp = BrotherStateData.GetMaxHp(player);
        GD.Print($"Calculating base heal amount for Brother: Max HP = {maxHp}, Base Heal = {maxHp * 0.3m * 2}");
		return maxHp * 0.3m * 2;
	}

    public static void PlayRestSiteHealSfx()
	{
		NDebugAudioManager.Instance?.Play("sleep.tres");
		NDebugAudioManager.Instance?.Play("SOTE_SFX_SleepBlanket_v1.mp3", 1f, PitchVariance.Small);
	}

    public static async Task ExecuteRestSiteHeal(Player player)
    {
        BrotherStateData.Heal(player, (int)GetHealAmount(player));
    }
}