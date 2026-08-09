using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer; // 提供 PlayerChoiceContext
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Pluma.Scripts;

// 破绽感知：每层使拥有创伤的敌人造成的攻击伤害减半。回合结束时移除。
[RegisterPower]
public class FlawSensePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/FlawSense.png",
        BigIconPath: "res://pluma/images/powers/FlawSense.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DynamicVar("DamageReduction", 0.5m)
    };

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != base.Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        if (dealer == null) return 1m;
        if (!dealer.HasPower<OpenWoundPower>()) return 1m;

        return DynamicVars["DamageReduction"].BaseValue;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        // 在自己的回合结束时移除该能力
        if (participants.Contains(base.Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}