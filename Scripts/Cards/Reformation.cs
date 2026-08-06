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

// 改变思维：2费能力牌，获得5层源源不断和5层混乱。本能。升级后费用-1。
[RegisterCard(typeof(PlumaCardPool))]
public class Reformation : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };

    public Reformation() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 施加5层源源不断
        await PowerCmd.Apply<ConstantFlowPower>(
            choiceContext, base.Owner.Creature, 5, base.Owner.Creature, this);
        // 施加5层混乱
        await PowerCmd.Apply<MindRotPower>(
            choiceContext, base.Owner.Creature, 5, base.Owner.Creature, this);
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<ConstantFlowPower>(),
        HoverTipFactory.FromPower<MindRotPower>()
    };

    
    protected override void OnUpgrade()
    {
        // 费用 -1
        base.EnergyCost.UpgradeBy(-1);
    }
}