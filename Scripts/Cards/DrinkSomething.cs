using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 随便喝点啥：1费罕见技能牌。获得1张随机基酒，并抽1张牌。升级后抽2张。
[RegisterCard(typeof(PlumaCardPool))]
public class DrinkSomething : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 抽牌数量动态变量：基础1，升级后2
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new CardsVar(1)
    };

    // 悬浮提示：基酒关键词
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit)
    };

    public DrinkSomething() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 1. 随机获得一张基酒（使用同步随机源，保证多人一致）
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        CardModel baseSpirit = rng.NextInt(6) switch
        {
            0 => base.CombatState.CreateCard<Gin>(player),
            1 => base.CombatState.CreateCard<Tequila>(player),
            2 => base.CombatState.CreateCard<Whiskey>(player),
            3 => base.CombatState.CreateCard<Rum>(player),
            4 => base.CombatState.CreateCard<Vodka>(player),
            _ => base.CombatState.CreateCard<Brandy>(player),
        };
        await CardPileCmd.AddGeneratedCardsToCombat(new[] { baseSpirit }, PileType.Hand, player);

        // 2. 抽牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 抽牌 1 → 2
    }
}