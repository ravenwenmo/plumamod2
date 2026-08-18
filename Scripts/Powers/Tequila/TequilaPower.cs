using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 伺机而动：饮用基酒时，获得一点力量；饮用鸡尾酒时，增加攻击段数
// 同时用于检测龙舌兰生命值增减并在TequilaState中更新
[RegisterPower]
public class TequilaPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Tequila.png",
        BigIconPath: "res://pluma/images/powers/Tequila.png"
    );

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal _, ValueProp props, Creature? __)
    {
        if (!(Owner.Monster as Monsters.Tequila).DieForYou)
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

        Entry.TequilaStateData.Modify(creature.PetOwner, state => {
            state.Hp = Math.Max(1, creature.CurrentHp);
            state.MaxHp = Math.Max(1, creature.MaxHp);
            });

        GD.Print($"[TequilaPower] TequilaStateDataChanged: Hp: {Entry.TequilaStateData.Get(creature.PetOwner).Hp}, MaxHp: {Entry.TequilaStateData.Get(creature.PetOwner).MaxHp}");
    }    
}