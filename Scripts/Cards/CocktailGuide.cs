using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 多索雷斯调酒指南：1费稀有能力牌。获得调酒指南能力。升级后固有。
[RegisterCard(typeof(PlumaCardPool))]
public class CocktailGuide : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 悬浮提示：鸡尾酒关键词
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.Cocktail)
    };

    public CocktailGuide() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<CocktailGuidePower>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate); // 固有
    }
}