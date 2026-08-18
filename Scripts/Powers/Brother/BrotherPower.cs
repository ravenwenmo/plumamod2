using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 伺机而动：饮用基酒时，获得一点力量；饮用鸡尾酒时，增加攻击段数
// 同时用于检测龙舌兰生命值增减并在BrotherStateData中更新；龙舌兰死亡时重置持久化状态
[RegisterPower]
public class BrotherPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Brother.png",
        BigIconPath: "res://pluma/images/powers/Brother.png"
    );

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal _, ValueProp props, Creature? __)
    {
        if (!(Owner.Monster as Monsters.Brother).DieForYou)
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

        BrotherStateData.SetHp(creature.PetOwner, creature.CurrentHp, creature.MaxHp);

        GD.Print($"[BrotherPower] BrotherStateDataChanged: Hp: {BrotherStateData.GetHp(creature.PetOwner)}, MaxHp: {BrotherStateData.GetMaxHp(creature.PetOwner)}");
    }

    // 实时检查：龙舌兰的力量层数变化时，同步持久化状态；
    // 达到阈值时立即切换为攻击循环意图（不依赖回合开始钩子，
    // 例如通过控制台直接给龙舌兰加力量也能即时触发）。
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        GD.Print("检测到能力层数发生变化！！！！！！！！！！！！！！！！");
        // 只关心龙舌兰自身的力量变化
        if (power is not StrengthPower || power.Owner != Owner) return;
        if (Owner.Monster is not Monsters.Brother brother) return;
        GD.Print("检测到力量层数发生变化！！！！！！！！！！！！！！！！");
        // 同步持久化状态中的力量值（含通过控制台等外部方式施加的力量）。
        // 注意：层数归零（amount <= 0）时不同步——
        // 战斗结束清理力量也会触发此钩子，若同步会把强化循环应继承的力量清零。
        if (amount > 0)
        {
            Entry.BrotherStateData.Modify(Owner.PetOwner, s => s.Strength = (int)amount);
            BrotherStateData.SyncStrength(Owner.PetOwner, (int)amount);
            GD.Print("同步力量层数！！！！！！！！！！！！！！！！");
        }
        if (power.Amount < Brother.STRENGTH_THRESHOLD)
        {
            GD.Print($"力量层数没有达到阈值！！！！！！！！！！！！！！！！当前 {power.Amount}");
            return;
        }

        GD.Print($"[BrotherPower] Strength reached {power.Amount} (threshold {Brother.STRENGTH_THRESHOLD}), switching to attack intent");
        await brother.SwitchToAttackIntent();
    }
}