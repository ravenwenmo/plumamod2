using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
namespace Pluma.Scripts;

// 调酒之韵：每当你打出一张“辅料组合包”时，抽等同于能力层数的牌。
[RegisterPower]
public class BartenderRhymePower : ModPowerTemplate
{
    // 卡面文本回退用的默认抽牌数（MixerPack.DisplayDrawCount 会读它）。
    // 实际抽牌以能力层数（base.Amount）为准。
    public const int ExtraDrawPerMixerPack = 1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 不可叠加，层数表示额外抽牌数

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BartenderRhymePower.png",
        BigIconPath: "res://pluma/images/powers/BartenderRhymePower.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只响应持有者打出的辅料组合包
        if (cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card is MixerPack)
        {
            await CardPileCmd.Draw(
                choiceContext,
                base.Amount,   // 层数即额外抽牌数
                base.Owner.Player
            );
        }
    }
}