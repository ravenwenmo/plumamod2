using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 放空：手牌中所有「本能」牌本回合费用变为 0。升级后 1 费 → 0 费。
// （历史：曾经是"失去所有渐入佳境"，后来改成"消耗所有非本能牌换能量与抽牌"，现在改为减费。）
[RegisterCard(typeof(PlumaCardPool))]
public class EmptyMind : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能 + 消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };

    public EmptyMind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 手牌中所有本能牌（本牌打出时已不在手牌里，所以不会算到自己）
        var memoryCards = PileType.Hand.GetPile(player).Cards
            .Where(card => card.Keywords.Contains(MyKeywords.MuscleMemory))
            .ToList();

        foreach (var card in memoryCards)
        {
            if (!card.IsMutable) continue;

            // SetThisTurnOrUntilPlayed：本回合内、或打出该牌之前有效，
            // 回合结束 / 打出时由本体自动清理（LocalCostModifierExpiration.EndOfTurn | WhenPlayed）。
            card.EnergyCost.SetThisTurnOrUntilPlayed(0);
            // 本体加局部降费不会主动发这个事件，这里补一下，手牌上的费用数字才会立刻刷新。
            card.InvokeEnergyCostChanged();
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}
