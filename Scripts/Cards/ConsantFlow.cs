using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 源源不断：1费能力牌，获得1层源源不断。升级后获得固有。
[RegisterCard(typeof(PlumaCardPool))]
public class ConstantFlow : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public ConstantFlow() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ConstantFlowPower>(
            choiceContext, base.Owner.Creature, 1, base.Owner.Creature, this);
    }
    
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };


    protected override void OnUpgrade()
    {
        // 添加固有关键词
        AddKeyword(CardKeyword.Innate);
    }
}