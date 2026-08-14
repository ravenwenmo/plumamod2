using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 专业吧台：每两个玩家回合，将一张辅料组合包加入手牌。
[RegisterPower]
public class ProfessionalBarPower : ModPowerTemplate
{
    private int _turnCounter;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ProfessionalBar.png",
        BigIconPath: "res://pluma/images/powers/ProfessionalBar.png"
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;

        _turnCounter++;
        if (_turnCounter % 2 == 0)
        {
            var mixer = base.Owner.CombatState.CreateCard<MixerPack>(base.Owner.Player);
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixer }, PileType.Hand, base.Owner.Player);
        }
    }
}