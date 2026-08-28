using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 随机调酒组合：1费普通技能牌，消耗。「随机基酒 1」，并将1张辅料组合包放入弃牌堆。升级后辅料组合包升级。
[RegisterCard(typeof(PlumaCardPool))]
public class RandomMixologySet : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip,
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };

    public RandomMixologySet() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 「随机基酒 1」
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        var baseSpirits = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, 1, base.CombatState, rng);
        if (baseSpirits.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(baseSpirits, PileType.Hand, player);
        }

        // 创建辅料组合包，升级后为升级版
        var mixerPack = base.CombatState.CreateCard<MixerPack>(player);
        if (base.IsUpgraded)
        {
            CardCmd.Upgrade(mixerPack);
        }

        // 按 Dirge 塞灵魂的方式塞入弃牌堆，并播放牌堆插入预览特效
        CardPileAddResult discardResult = await CardPileCmd.AddGeneratedCardToCombat(
            mixerPack,
            PileType.Discard,
            player
        );

        CardCmd.PreviewCardPileAdd(new[] { discardResult });
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费// 升级效果在 OnPlay 中判断，无需额外逻辑
    }
}