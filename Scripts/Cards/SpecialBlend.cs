using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts.Cards;

// 特调：消耗两张手牌，获得2张不同的随机基酒。升级后费用-1。
[RegisterCard(typeof(PlumaCardPool))]
public class SpecialBlend : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public SpecialBlend() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 选择两张手牌消耗（不足两张时也消耗已选中的牌，没有牌则继续后续效果）
        var selected = await CardSelectCmd.FromHand(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 2),
            context: choiceContext,
            player: player,
            filter: null,
            source: this
        );

        var toExhaust = selected.ToList();
        foreach (var card in toExhaust)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 获得两张不同的随机基酒
        var availableBaseSpirits = new List<CardModel>
        {
            base.CombatState.CreateCard<Gin>(player),
            base.CombatState.CreateCard<Tequila>(player),
            base.CombatState.CreateCard<Whiskey>(player),
            base.CombatState.CreateCard<Rum>(player),
            base.CombatState.CreateCard<Vodka>(player),
            base.CombatState.CreateCard<Brandy>(player),
        };

        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        var generated = new List<CardModel>();

        for (int i = 0; i < 2; i++)
        {
            if (availableBaseSpirits.Count == 0) break;

            int index = rng.NextInt(availableBaseSpirits.Count);
            generated.Add(availableBaseSpirits[index]);
            availableBaseSpirits.RemoveAt(index);
        }

        if (generated.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, player);
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.MuscleMemory),
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit)
    };

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}