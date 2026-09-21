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

// 人镰合一：1费本能技能，选择手牌，将其变为切割（可选手牌不足时按实际数量转化，至少 1 张）。
// 升级后额外选择并消耗1张手牌，生成对应数量的切割。
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
        int handCount = handPile.Cards.Count;
        if (handCount == 0) return; // 无手牌则不做任何事

        int selectCount = DynamicVars["CardsToConvert"].IntValue;

        // 选择范围用「至少 1 张、至多 min(要变的张数, 手牌数)」：
        //   · 手牌比 CardsToConvert 多时也允许少选：选够 1 张确认按钮就可用，
        //     不必被迫凑满 CardsToConvert 张（原版写死 Min=Max=selectCount 时做不到）；
        //   · 手牌不足 CardsToConvert 张时上限自动降到实际手牌数（只有 1 张手牌时直接自动选中）；
        //   · 选满上限依旧自动确认，手感与原版一致。
        int maxCount = Math.Min(selectCount, handCount);

        var selectPrompt = new LocString("cards", "PLUMA_CARD_SCYTHE_UNITY.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(selectPrompt, 1, maxCount),
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