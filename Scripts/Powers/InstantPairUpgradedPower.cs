using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 即时搭配（升级版）：每当持有者获得一张基酒牌时，将1张升级过的辅料组合包加入抽牌堆。
[RegisterPower]
public class InstantPairUpgradedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/InstantPairUpgradedPower.png",
        BigIconPath: "res://pluma/images/powers/InstantPairUpgradedPower.png"
    );

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is not IBaseSpiritCard || card.Owner?.Creature != base.Owner)
            return;

        var mixer = base.Owner.CombatState.CreateCard<MixerPack>(base.Owner.Player);

        // 升级辅料包
        CardCmd.Upgrade(new List<CardModel> { mixer }, CardPreviewStyle.None);

        // 将升级后的辅料组合包加入抽牌堆，并获得添加结果用于播放入堆特效
        CardPileAddResult drawResult = await CardPileCmd.AddGeneratedCardToCombat(
            mixer,
            PileType.Draw,
            base.Owner.Player,
            CardPilePosition.Random
        );

        // 播放牌堆插入预览特效
        CardCmd.PreviewCardPileAdd(new[] { drawResult });
    }
}