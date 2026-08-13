using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;


// 节奏感：每打出两张本能牌，获得 1 点能量。
[RegisterPower]
public class TheBeatPower : ModPowerTemplate
{
    // 本能牌计数
    private int _muscleMemoryCount;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TheBeatPower.png",
        BigIconPath: "res://pluma/images/powers/TheBeatPower.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner &&
            cardPlay.Card.Keywords.Contains(MyKeywords.MuscleMemory))
        {
            _muscleMemoryCount++;

            if (_muscleMemoryCount >= 2)
            {
                await PlayerCmd.GainEnergy(1, base.Owner.Player);
                _muscleMemoryCount = 0; // 重置计数，开始下一轮
            }
        }
    }
}