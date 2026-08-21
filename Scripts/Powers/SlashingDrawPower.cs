using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 每当打出切割时，抽等量于该能力层数的牌。
[RegisterPower]
public class SlashingDrawPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/SlashingDrawPower.png",
        BigIconPath: "res://pluma/images/powers/SlashingDrawPower.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只响应持有者打出的牌，并且必须是 Slashing 类型
        if (cardPlay.Card.Owner.Creature != base.Owner || cardPlay.Card.GetType() != typeof(Slashing))
            return;

        await CardPileCmd.Draw(choiceContext, (int)base.Amount, base.Owner.Player);
    }
}