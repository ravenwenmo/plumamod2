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

// 随机调酒组合：2费普通技能牌，消耗。「随机基酒 1」，并将1张辅料组合包放入弃牌堆。升级后费用减1。
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

    // 消耗关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip,
        HoverTipFactory.FromCard<MixerPack>()
    };
    
    public RandomMixologySet() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        // 「随机基酒 1」：统一走 BaseSpiritGeneration。
        // 多人同步：使用局内确定性随机源（各端同一序列），严禁 new Random()
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        var baseSpirits = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, 1, base.CombatState, rng);
        if (baseSpirits.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(baseSpirits, PileType.Hand, player);
        }

        // 将一张辅料组合包放入弃牌堆
        var mixerPack = base.CombatState.CreateCard<MixerPack>(player);
        await CardPileCmd.Add(mixerPack, PileType.Discard);
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}