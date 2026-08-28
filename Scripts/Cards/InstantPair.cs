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
using System.Collections.Generic;

namespace Pluma.Scripts;

// 即时搭配：1费罕见能力牌。每当获得一张基酒牌时，向抽牌堆加入1张辅料组合包。升级后加入的辅料包是升级过的。
[RegisterCard(typeof(PlumaCardPool))]
public class InstantPair : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );
    
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };

    public InstantPair() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (base.IsUpgraded)
        {
            await PowerCmd.Apply<InstantPairUpgradedPower>(
                choiceContext,
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            await PowerCmd.Apply<InstantPairPower>(
                choiceContext,
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                this
            );
        }
    }


    protected override void OnUpgrade()
    {
        // 升级效果已在 OnPlay 中判断
    }
}