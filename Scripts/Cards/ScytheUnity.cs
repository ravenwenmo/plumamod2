using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 人镰合一：1费本能技能，选择手牌，将其变为切割。升级后额外选择并消耗1张手牌，生成对应数量的切割。
[RegisterCard(typeof(PlumaCardPool))]
public class ScytheUnity : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能、消耗关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust,
        MyKeywords.MuscleMemory
    };

    // 动态变量：基础选择1张手牌，升级后+1
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("CardsToConvert", 1)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromCard<Slashing>()
    };

    public ScytheUnity() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);
        if (handPile.Cards.Count == 0) return;

        int selectCount = DynamicVars["CardsToConvert"].IntValue;

        var selectPrompt = new LocString("cards", "PLUMA_CARD_SCYTHE_UNITY.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(selectPrompt, selectCount),
            filter: null,
            source: this
        );

        var chosenCards = selected.ToList();
        if (chosenCards.Count == 0) return;

        // 消耗选中的牌
        foreach (var card in chosenCards)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 生成对应数量的切割牌
        foreach (var card in chosenCards)
        {
            var slashingCard = base.CombatState.CreateCard<Slashing>(player);
            if (slashingCard != null)
            {
                await CardPileCmd.AddGeneratedCardsToCombat(new[] { slashingCard }, PileType.Hand, player);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后选择手牌数量 +1
        DynamicVars["CardsToConvert"].UpgradeValueBy(1m);
    }
}