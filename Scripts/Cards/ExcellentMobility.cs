using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 优良机动：1费能力牌，获得优良机动能力（每获得1层渐入佳境触发一次格挡）。敲前每次2点格挡，敲后3点，不给固有。
[RegisterCard(typeof(PlumaCardPool))]
public class ExcellentMobility : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 每次触发获得的格挡：敲前 2，敲后 3
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new PowerVar<ExcellentMobilityPower>(2m)
    ];

    public ExcellentMobility() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ExcellentMobilityPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["ExcellentMobilityPower"].BaseValue,
            base.Owner.Creature,
            this
        );
    }
    
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    protected override void OnUpgrade()
    {
        // 不再添加"固有"；只把每次触发的格挡 2 → 3
        DynamicVars["ExcellentMobilityPower"].UpgradeValueBy(1m);
    }
}