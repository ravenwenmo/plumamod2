using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 调酒之韵：每当你打出一张辅料组合包时，抽1张牌。
[RegisterPower]
public class BartenderRhymePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single; // 不可叠加

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BartenderRhyme.png",
        BigIconPath: "res://pluma/images/powers/BartenderRhyme.png"
    );
    
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只处理持有者打出的辅料组合包
        if (cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card is MixerPack)
        {
            await CardPileCmd.Draw(choiceContext, 1, base.Owner.Player);
        }
    }
}