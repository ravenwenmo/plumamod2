using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 风中之羽：每打出一张攻击牌，获得等量于能力层数的渐入佳境。
[RegisterPower]
public class WindFeatherPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/WindFeatherPower.png",
        BigIconPath: "res://pluma/images/powers/WindFeatherPower.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != base.Owner) return;
        if (cardPlay.Card.Type != CardType.Attack) return;
        if (base.Amount <= 0) return;

        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner,
            base.Amount,
            base.Owner,
            cardPlay.Card
        );
    }
}