using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts;

// 专业吧台（升级版）：每两个玩家回合，将一张升级过的辅料组合包加入手牌。
[RegisterPower]
public class ProfessionalBarUpgradedPower : ModPowerTemplate
{
    private int _turnCounter;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ProfessionalBarUpgradedPower.png",
        BigIconPath: "res://pluma/images/powers/ProfessionalBarUpgradedPower.png"
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;

        _turnCounter++;
        if (_turnCounter % 2 == 0)
        {
            var mixer = base.Owner.CombatState.CreateCard<MixerPack>(base.Owner.Player);
            CardCmd.Upgrade(new List<CardModel> { mixer }, CardPreviewStyle.None);
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixer }, PileType.Hand, base.Owner.Player);
        }
    }
}