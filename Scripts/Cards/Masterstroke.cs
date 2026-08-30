using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 大师斩：2费攻击牌，造成等于渐入佳境层数的伤害（实时显示）。升级后费用-1。
[RegisterCard(typeof(PlumaCardPool))]
public class Masterstroke : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 仿照 TimesUp：基础伤害 0，额外伤害 1，乘数为玩家身上的渐入佳境层数
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(0m),
        new ExtraDamageVar(1m),
        ModCardVars.Int("FlowStateAmount", 1),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, target) =>
            card?.Owner?.Creature?.GetPowerAmount<FlowState>() ?? 0
        )


    };
    
    // 本能关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory
    };

    public Masterstroke() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["FlowStateAmount"].BaseValue,   // 3 层（升级后变为 4 层）
            base.Owner.Creature,
            this
        );
        await DamageCmd.Attack(base.DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    protected override void OnUpgrade()
    {
        //base.EnergyCost.UpgradeBy(-1); // 3费 → 2费
        DynamicVars["FlowStateAmount"].UpgradeValueBy(1m);
    }
}