using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 复读：1费稀有技能，本能，消耗，虚无。选择一张手牌，复制它并给复制品添加本能关键词。升级后移除虚无。
[RegisterCard(typeof(PlumaCardPool))]
public class Repeat : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 关键词：本能、消耗、虚无（升级后移除虚无）
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust,
        CardKeyword.Ethereal
    };

    public Repeat() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);
        if (handPile.Cards.Count == 0) return;

        var selectPrompt = new LocString("cards", "PLUMA_CARD_REPEAT.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(selectPrompt, 1),
            filter: null,
            source: this
        );

        var original = selected.FirstOrDefault();
        if (original == null) return;

        // 使用 CreateClone 复制卡牌
        CardModel copy = original.CreateClone();

        // 给复制品添加本能关键词
        CardCmd.ApplyKeyword(copy, MyKeywords.MuscleMemory);

        // 将复制品加入手牌
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
    }

    protected override void OnUpgrade()
    {
        // 移除虚无关键词
        RemoveKeyword(CardKeyword.Ethereal);
    }
}