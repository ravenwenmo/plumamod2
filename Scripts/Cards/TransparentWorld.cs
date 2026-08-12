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

// 通透世界：3费能力牌，获得通透世界、2层混乱、3层源源不断。虚无（升级后移除）。
[RegisterCard(typeof(PlumaCardPool))]
public class TransparentWorld : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };

    public TransparentWorld() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 通透世界标记
        await PowerCmd.Apply<TransparentWorldPower>(
            choiceContext, base.Owner.Creature, 1, base.Owner.Creature, this);
        // 2层混乱（抽牌-2）
        /*
        await PowerCmd.Apply<MindRotPower>(
            choiceContext, base.Owner.Creature, 2, base.Owner.Creature, this);
        // 3层源源不断（每回合3层渐入佳境）
        await PowerCmd.Apply<ConstantFlowPower>(
            choiceContext, base.Owner.Creature, 3, base.Owner.Creature, this);
        */
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}