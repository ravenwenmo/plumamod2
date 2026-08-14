using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace Pluma.Scripts;

// 暗示：1费稀有技能，本能，从抽牌堆选择1张牌添加本能。升级后费用减1。
[RegisterCard(typeof(PlumaCardPool))]
public class Hint : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };

    public Hint() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var drawPile = PileType.Draw.GetPile(player);
        if (drawPile.IsEmpty) return;

        // 从抽牌堆选择一张牌，过滤掉已经拥有本能关键词的牌
        var selected = await CardSelectCmd.FromCombatPile(
            prefs: new CardSelectorPrefs(
                new LocString("cards", "PLUMA_CARD_HINT.selectPrompt"),
                1
            ),
            context: choiceContext,
            pile: drawPile,
            player: player,
            filter: card => !card.Keywords.Contains(MyKeywords.MuscleMemory)  // 排除已有本能的牌
        );

        var targetCard = selected.FirstOrDefault();
        if (targetCard != null)
        {
            // 添加本能关键词
            CardCmd.ApplyKeyword(targetCard, MyKeywords.MuscleMemory);
        }
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}