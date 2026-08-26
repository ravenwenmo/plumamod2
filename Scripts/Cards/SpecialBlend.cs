using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts.Cards;

// 特调：消耗两张手牌，「随机基酒 2」。升级后费用-1。
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

        // 「随机基酒 2」：统一走 BaseSpiritGeneration（两张不重复，优先排除手牌已有种类）
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        var generated = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, 2, base.CombatState, rng);

        if (generated.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, player);
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.MuscleMemory),
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip
    };

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}