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

namespace Pluma.Scripts;

// 截然相反的集中：2费攻击牌，造成14点伤害，渐入佳境在本攻击上发挥5倍效果（升级后10倍）。
[RegisterCard(typeof(PlumaCardPool))]
public class ButFocused : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 基础伤害0，额外伤害14，乘数=1 + 渐入佳境层数 * 倍率 * 3%
    // 倍率通过 Multiplier 变量管理（5→10）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(0m),
        new ExtraDamageVar(14m),
        ModCardVars.Int("Multiplier", 5),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, target) =>
        {
            if (card == null) return 1m;
            var stacks = card.Owner?.Creature?.GetPowerAmount<FlowState>() ?? 0;
            // 安全获取 Multiplier 变量，如果尚未初始化则使用基础值 5
            var multiplier = card.DynamicVars.TryGetValue("Multiplier", out var dynVar)
                ? dynVar.BaseValue
                : 5m;
            return 1m + stacks * multiplier * 0.03m;
        })
    };

    public ButFocused() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Multiplier"].UpgradeValueBy(5m); // 5 → 10
    }
}