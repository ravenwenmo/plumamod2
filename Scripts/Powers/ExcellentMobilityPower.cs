using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 优良机动：每拥有一层此能力，每当获得渐入佳境时，获得对应层数的格挡。
[RegisterPower]
public class ExcellentMobilityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ExcellentMobility.png",
        BigIconPath: "res://pluma/images/powers/ExcellentMobility.png"
    );

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 只响应渐入佳境层数变化，且目标是自己，且层数增加
        if (power is FlowState && power.Owner == base.Owner && amount > 0)
        {
            // 每层能力提供1点格挡
            await CreatureCmd.GainBlock(
                base.Owner,
                base.Amount,
                ValueProp.Unpowered,
                null,
                fast: true
            );
        }
    }
}