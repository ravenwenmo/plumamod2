using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 放手一搏：弃掉所有手牌，获得等量层数的渐入佳境。本能，消耗。升级后移除消耗。
[RegisterCard(typeof(PlumaCardPool))]
public class DesperateStrike : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon; // 可根据需要调整
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 关键词：本能，消耗（升级后移除消耗）
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };

    public DesperateStrike() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        if (player == null) return;

        var handPile = PileType.Hand.GetPile(player);
        var handCards = handPile.Cards.ToList();
        int discardedCount = handCards.Count;

        if (discardedCount > 0)
        {
            // 弃掉所有手牌（仿 CalculatedGamble 的弃牌方法）
            await CardCmd.DiscardAndDraw(choiceContext, handCards, cardsToDraw: 0);

            // 获得等量层数的渐入佳境
            await PowerCmd.Apply<FlowState>(
                choiceContext,
                base.Owner.Creature,
                discardedCount,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        // 移除消耗关键词（仿 VoidForm）
        RemoveKeyword(CardKeyword.Exhaust);
    }
}