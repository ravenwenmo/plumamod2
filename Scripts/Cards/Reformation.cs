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
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 改变思维：2费能力牌，获得源源不断和混乱。本能。升级后费用-1，且源源不断层数+1。
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

    // 变量：基础 4 层源源不断，5 层混乱
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("ConstantFlowAmount", 4),
        ModCardVars.Int("MindRotAmount", 5)
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };

    public Reformation() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ConstantFlowPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["ConstantFlowAmount"].BaseValue,
            base.Owner.Creature,
            this
        );

        await PowerCmd.Apply<MindRotPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["MindRotAmount"].BaseValue,
            base.Owner.Creature,
            this
        );
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<ConstantFlowPower>(),
        HoverTipFactory.FromPower<MindRotPower>()
    };

    protected override void OnUpgrade()
    {
        // 费用 -1
        //base.EnergyCost.UpgradeBy(-1);

        // 源源不断层数 +1：4 → 5
        DynamicVars["ConstantFlowAmount"].UpgradeValueBy(1m);
    }
}