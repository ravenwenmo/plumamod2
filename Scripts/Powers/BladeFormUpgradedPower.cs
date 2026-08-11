using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts;

// 利刃形态（基础）：每回合开始获得一张随机攻击牌，本回合免费且附带切割关键词。
[RegisterPower]
public class BladeFormUpgradedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BladeFormUpgradePower.png",
        BigIconPath: "res://pluma/images/powers/BladeFormUpgradePower.png"
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;

        // 从角色卡池中随机生成一张攻击牌
        var card = CardFactory.GetDistinctForCombat(
            base.Owner.Player,
            from c in base.Owner.Player.Character.CardPool.GetUnlockedCards(
                base.Owner.Player.UnlockState,
                base.Owner.Player.RunState.CardMultiplayerConstraint)
            where c.Type == CardType.Attack
            select c,
            1,
            base.Owner.Player.RunState.Rng.CombatCardGeneration
        ).FirstOrDefault();

        if (card == null) return;

        // 添加切割关键词
        CardCmd.ApplyKeyword(card, MyKeywords.Slashing);
        CardCmd.ApplyKeyword(card, CardKeyword.Exhaust);
        // 在 card.SetToFreeThisTurn(); 之前加入
        CardCmd.Upgrade(new List<CardModel> { card }, CardPreviewStyle.None);
        // 本回合免费
        card.SetToFreeThisTurn();

        // 添加到手牌
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, base.Owner.Player);
    }
}