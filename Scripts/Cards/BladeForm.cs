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

// 利刃形态：3费，本能，每回合获得一张免费随机攻击牌并附带切割。升级后获得的牌是升级版。
[RegisterCard(typeof(PlumaCardPool))]
public class BladeForm : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        base.IsUpgraded
            ? HoverTipFactory.FromPower<BladeFormUpgradedPower>()
            : HoverTipFactory.FromPower<BladeFormPower>()
    };

    public BladeForm() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (base.IsUpgraded)
        {
            await PowerCmd.Apply<BladeFormUpgradedPower>(
                choiceContext,
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            await PowerCmd.Apply<BladeFormPower>(
                choiceContext,
                base.Owner.Creature,
                1,
                base.Owner.Creature,
                this
            );
        }
    }
    /*
    protected override void OnUpgrade()
    {
        // 效果在 OnPlay 中根据 IsUpgraded 选择能力
    }
    */
}