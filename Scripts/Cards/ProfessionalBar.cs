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

// 专业吧台：1费罕见能力牌。每两个回合获得一张辅料组合包。升级后改为获得升级过的辅料组合包。
[RegisterCard(typeof(PlumaCardPool))]
public class ProfessionalBar : ModCardTemplate
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
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };
    
    public ProfessionalBar() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (base.IsUpgraded)
        {
            await PowerCmd.Apply<ProfessionalBarUpgradedPower>(
                choiceContext,
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            await PowerCmd.Apply<ProfessionalBarPower>(
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
        // 效果在 OnPlay 中判断
    }
}