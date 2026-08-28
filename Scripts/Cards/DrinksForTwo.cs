using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts.Cards;

// 我喝两杯：2费罕见技能，本能，消耗。「随机基酒 2」，将1张辅料组合包放入抽牌堆和弃牌堆。升级后费用减1。
[RegisterCard(typeof(PlumaCardPool))]
public class DrinksForTwo : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory, // 本能
        CardKeyword.Exhaust
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip,
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };

    public DrinksForTwo() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;

        // 「随机基酒 2」：统一走 BaseSpiritGeneration（两张不重复，优先排除手牌已有种类）
        var baseSpirits = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, 2, base.CombatState, rng);
        if (baseSpirits.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(baseSpirits, PileType.Hand, player);
        }

        // 分别将两张辅料组合包放入抽牌堆和弃牌堆
        var mixerToDraw = base.CombatState.CreateCard<MixerPack>(player);
        var mixerToDiscard = base.CombatState.CreateCard<MixerPack>(player);
        if (base.IsUpgraded)
        {
            CardCmd.Upgrade(mixerToDraw);
            CardCmd.Upgrade(mixerToDiscard);
        }

// 使用 AddGeneratedCardToCombat 获取结果，以便播放牌堆插入预览特效
        CardPileAddResult drawResult = await CardPileCmd.AddGeneratedCardToCombat(
            mixerToDraw,
            PileType.Draw,
            player,
            CardPilePosition.Random
        );

        CardPileAddResult discardResult = await CardPileCmd.AddGeneratedCardToCombat(
            mixerToDiscard,
            PileType.Discard,
            player
        );

// 播放两张牌分别飞入抽牌堆和弃牌堆的动画
        CardCmd.PreviewCardPileAdd(new[] { drawResult, discardResult });
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 2费 → 1费
    }
}