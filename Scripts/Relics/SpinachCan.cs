using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts;

// 绿叶菜罐头：获得渐入佳境时，额外获得1层活力。
[RegisterRelic(typeof(PlumaRelicPool))]
public class SpinachCan : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon; // 可根据需要调整稀有度

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    // 不修改层数，只做检测用
    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource)
    {
        // 不影响渐入佳境的层数
        return 0m;
    }

    // 在层数被修改后触发（包括施加和增加）
    public override async Task AfterModifyingPowerAmountGiven(PowerModel power)
    {
        // 只处理给予玩家自己的渐入佳境
        if (power is FlowState && power.Owner == base.Owner.Creature)
        {
            Flash();
            // 使用空上下文施加活力（不需要玩家选择）
            await PowerCmd.Apply<VigorPower>(
                new ThrowingPlayerChoiceContext(),
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                null
            );
        }
    }
}