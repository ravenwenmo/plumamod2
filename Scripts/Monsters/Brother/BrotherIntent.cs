using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace Pluma.Monsters;

public class BrotherBuffIntent : BuffIntent
{
	protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString locString = new LocString("intents", "PLUMA_BROTHER_BUFF" + ".description");
        ICombatState? combatState = owner.CombatState;
        locString.Add("IsMultiplayer", combatState != null && combatState.RunState.Players.Count > 1);
        return locString;
    }
}

public class BrotherAttackIntent : AttackIntent
{
	private readonly int _repeat;

	private readonly Func<int>? _repeatCalc;

	protected override LocString IntentLabelFormat => new LocString("intents", "FORMAT_DAMAGE_MULTI");

	public override int Repeats => _repeatCalc?.Invoke() ?? _repeat;

	public BrotherAttackIntent(int damage, int repeat)
	{
		base.DamageCalc = () => damage;
		_repeat = repeat;
	}

	public BrotherAttackIntent(int damage, Func<int> repeatCalc)
	{
		base.DamageCalc = () => damage;
		_repeatCalc = repeatCalc;
	}

	public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
	{
		return GetDamage(targets, owner) * Repeats;
	}

	protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
	{
		IEnumerable<Creature> enemys = owner.CombatState.GetOpponentsOf(owner);
		LocString locString = new("intents", "PLUMA_BROTHER_ATTACK" + ".description");
		locString.Add("Damage", GetDamage(enemys, owner));
        locString.Add("Repeat", Repeats);
		return locString;
	}

	public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
	{
		IEnumerable<Creature> enemys = owner.CombatState.GetOpponentsOf(owner);
		LocString intentLabelFormat = IntentLabelFormat;
		intentLabelFormat.Add("Damage", GetDamage(enemys, owner));
		intentLabelFormat.Add("Repeat", Repeats);
		return intentLabelFormat;
	}

	public int GetDamage(IEnumerable<Creature> targets, Creature owner)
	{
		decimal num = DamageCalc();

		Player? me = owner.PetOwner ?? LocalContext.GetMe(owner.CombatState);
		if (me == null || me.Creature == null || me.RunState == null)
		{
			// 预览/初始化阶段玩家上下文不完整时，返回未经修正的基础伤害
			return Math.Max(0, (int)num);
		}

		if (targets != null && targets.Count() == 1)
		{
			Creature mo = targets.First();
			num = Hook.ModifyDamage(
				me.RunState,
				me.Creature.CombatState,
				mo,
				owner,
				DamageCalc(),
				ValueProp.Move,
				null,
				null,
				ModifyDamageHookType.All,
				CardPreviewMode.None,
				out IEnumerable<AbstractModel> _
			);
		}
		else
		{
			num = Hook.ModifyDamage(
				me.RunState,
				me.Creature.CombatState,
				null,
				owner,
				DamageCalc(),
				ValueProp.Move,
				null,
				null,
				ModifyDamageHookType.All,
				CardPreviewMode.None,
				out IEnumerable<AbstractModel> _
			);
		}

		return Math.Max(0, (int)num);
	}
	/*
	public int GetDamage(IEnumerable<Creature> targets, Creature owner)
	{
		decimal num = DamageCalc();
		Player me = LocalContext.GetMe(owner.CombatState);
		if (targets.Count() == 1)
		{
			Creature mo = targets.First();
			num = Hook.ModifyDamage(me.RunState, me.Creature.CombatState, mo, owner, DamageCalc(), ValueProp.Move, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out IEnumerable<AbstractModel> _);
		} else
		{
			num = Hook.ModifyDamage(me.RunState, me.Creature.CombatState, null, owner, DamageCalc(), ValueProp.Move, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out IEnumerable<AbstractModel> _);
		}

		return Math.Max(0, (int)num);
	}
	*/
}