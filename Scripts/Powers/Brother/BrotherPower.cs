using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 解放：特性达到一定层数时，进入数回合的攻击意图。
// 同时用于检测龙舌兰生命值增减并在BrotherStateData中更新；龙舌兰死亡时重置持久化状态
[RegisterPower]
public class BrotherPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static int TraitThreshold => Brother.TraitThreshold;
    public static int AttackIntentTurns { get; set; } = Brother.ATTACK_INTENT_TURNS;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("TraitThreshold", TraitThreshold),
        new DynamicVar("AttackIntentTurns", AttackIntentTurns)
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Brother.png",
        BigIconPath: "res://pluma/images/powers/Brother.png"
    );

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal unblockedDamage, ValueProp props, Creature? _)
    {
        if (!(Owner.Monster as Brother).DieForYou && unblockedDamage < Owner.PetOwner.Creature.CurrentHp)
        {
            return target;
        }

        if (target != base.Owner.PetOwner?.Creature)
        {
            return target;
        }

        if (base.Owner.IsDead)
        {
            return target;
        }

        if (!props.IsPoweredAttack())
        {
            return target;
        }

        return base.Owner;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner) return;

        if (!creature.IsAlive)
        {
            BrotherStateData.SetDead(creature.PetOwner);
            return;
        }

        BrotherStateData.SetFromBrother(creature.PetOwner, creature);

        GD.Print($"[BrotherPower] BrotherStateDataChanged: Hp: {BrotherStateData.GetHp(creature.PetOwner)}, MaxHp: {BrotherStateData.GetMaxHp(creature.PetOwner)}");
    }

    // 战斗结束时再次更新，确保保存的状态是最新的
    public override async Task AfterCombatVictory(CombatRoom combatRoom)
    {
        BrotherStateData.SetFromBrother(Owner.PetOwner, Owner);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is BrotherAttackTurnsPower)
        {
            BrotherStateData.SetAttackTurnsRemaining(Owner.PetOwner, Owner.GetPowerAmount<BrotherAttackTurnsPower>());
            return;
        }

        // 只关心龙舌兰自身的特性变化
        if (power is not TraitPower || power.Owner != Owner) return;
        if (Owner.Monster is Brother brother)
        {
            BrotherStateData.SetTrait(Owner.PetOwner, Owner.GetPowerAmount<TraitPower>());
            await brother.TriggerWhenGainTrait();
        }
        else
        {
            throw new Exception("BrotherPower: Owner is not Tequila.");
        }
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (creature == Owner.PetOwner?.Creature)
        {
            await CreatureCmd.Kill(Owner, true);
        }
    }
}