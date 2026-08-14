using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 物尽其用：每当打出一张“辅料组合包”时，获得等同于能力层数的额外格挡。
[RegisterPower]
public class MakeUseOfEverythingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single; // 不可叠加，层数表示格挡值

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/MakeUseOfEverything.png",
        BigIconPath: "res://pluma/images/powers/MakeUseOfEverything.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 只响应持有者打出的辅料组合包
        if (cardPlay.Card.Owner.Creature == base.Owner && cardPlay.Card is MixerPack)
        {
            await CreatureCmd.GainBlock(
                base.Owner,
                base.Amount,                 // 层数即额外格挡值
                ValueProp.Unpowered,
                cardPlay
            );
        }
    }
}