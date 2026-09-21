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
using MegaCrit.Sts2.Core.Rewards;
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
        // 羽毛笔的“休息”只回龙舌兰的血，羽毛笔自己一点血都不回（见 ReaperBadge/AquaDawn）。
        // 但“休息”事件必须像原版 HealRestSiteOption 那样广播出去，
        // 否则所有挂在休息上的原版内容都会失效：
        //   · 石炉加湿器：+5 最大生命 —— 已由 StoneHumidifierBrotherPatch 改加给龙舌兰；
        //   · 帝王枕头：休息回血 +15 —— 已由 BrotherRestSite.ModifyRestHealAmount 加给龙舌兰；
        //   · 睡梦捕手、小邮箱：休息后发奖励 —— 这是羽毛笔自己的奖励，仍按原版发；
        //   · BrotherRelic.AfterRestSiteHeal：龙舌兰回血 60%（见 BrotherRelic.cs，
        //     所以这里不能再自己调 ExecuteRestSiteHeal，否则龙舌兰回两次血）。
        await Hook.AfterRestSiteHeal(Owner.RunState, Owner, isMimicked: false);

        // 复刻原版休息奖励结算（Hook 改奖励 + 发放）。
        // 注意：不要改成 HealRestSiteOption.ExecuteRestSiteHeal——它除了广播 Hook，
        // 还会先按 Hook.ModifyRestSiteHealAmount 给羽毛笔回血；
        // ReaperBadge 的“归零”只是在遍历里返回 0，后面若还有帝王枕头(+15)之类的修正，
        // 血量就会被加回来，和“无法在火堆处回复生命”矛盾。
        List<Reward> rewards = new List<Reward>();
        Hook.ModifyRestSiteHealRewards(Owner.RunState, Owner, rewards, isMimicked: false);
        await RewardsCmd.OfferCustom(Owner, rewards);
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
		// 基数 60% 龙舌兰最大生命，再叠加原版“休息回血修正”类遗物的加成（帝王枕头 +15 等）：
		// 羽毛笔的休息对象是龙舌兰，所以这些加成也归龙舌兰。
		decimal healAmount = BrotherRestSite.ModifyRestHealAmount(player, GetBaseHealAmount(player));
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