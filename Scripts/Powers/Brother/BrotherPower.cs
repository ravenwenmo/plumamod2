using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 伺机而动：力量达到一定层数时，进入数回合的攻击意图。
// 同时用于检测龙舌兰生命值增减并在BrotherStateData中更新；龙舌兰死亡时重置持久化状态
[RegisterPower]
public class BrotherPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Brother.png",
        BigIconPath: "res://pluma/images/powers/Brother.png"
    );

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal _, ValueProp props, Creature? __)
    {
        if (!(Owner.Monster as Brother).DieForYou)
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
            // 龙舌兰死亡：重置持久化状态，下次召唤按默认状态（满血、0力量、强化意图）开始
            BrotherStateData.ResetToDefault(creature.PetOwner);
            return;
        }

        BrotherStateData.SetFromBrother(creature.PetOwner, creature.Monster as Brother);

        GD.Print($"[BrotherPower] BrotherStateDataChanged: Hp: {BrotherStateData.GetHp(creature.PetOwner)}, MaxHp: {BrotherStateData.GetMaxHp(creature.PetOwner)}");
    }

    // 无需实时更新龙舌兰力量，战斗结束时更新即可
    public override async Task AfterCombatVictory(CombatRoom combatRoom)
    {
        BrotherStateData.SetFromBrother(Owner.PetOwner, Owner.Monster as Brother);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只关心龙舌兰自身的力量变化
        if (power is not StrengthPower || power.Owner != Owner) return;
        if (Owner.Monster is Brother brother)
        {
            await brother.TriggerWhenGainStrength();
        } else
        {
            throw new Exception("BrotherPower: Owner is not Tequila.");
        }
    }
}