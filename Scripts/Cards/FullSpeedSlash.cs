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

// 全速斩击：2费攻击牌，造成3点伤害2次，额外造成渐入佳境层数一半的段数。升级后伤害变为4。
[RegisterCard(typeof(PlumaCardPool))]
public class FullSpeedSlash : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common; // 可根据需要调整
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(3m, ValueProp.Move)
    };

    public FullSpeedSlash() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        decimal baseDamage = DynamicVars.Damage.BaseValue;

        // 基础3次攻击
        for (int i = 0; i < 3; i++)
        {
            await DamageCmd.Attack(baseDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }

        // 额外段数 = 渐入佳境层数 / 2（向下取整）
        int flowStacks = (int)base.Owner.Creature.GetPowerAmount<FlowState>();
        int extraHits = flowStacks / 2;

        for (int i = 0; i < extraHits; i++)
        {
            await DamageCmd.Attack(baseDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>(),
    };
    
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m); // 伤害 3 → 4
    }
}