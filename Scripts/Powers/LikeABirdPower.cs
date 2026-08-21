using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 如鸟一般：受到攻击时，获得1层渐入佳境。
[RegisterPower]
public class LikeABirdPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加，每层触发一次
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/LikeABirdPower.png",
        BigIconPath: "res://pluma/images/powers/LikeABirdPower.png"
    );

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 只处理持有者受到的伤害
        if (target != base.Owner) return;

        // 每层能力获得1层渐入佳境
        if (base.Amount > 0)
        {
            await PowerCmd.Apply<FlowState>(
                choiceContext,
                base.Owner,
                base.Amount,
                base.Owner,
                null
            );
        }
    }
}