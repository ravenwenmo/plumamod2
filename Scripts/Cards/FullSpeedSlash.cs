
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 全速斩击：2费攻击牌，造成3点伤害2次，额外造成渐入佳境层数一半的段数。升级后伤害变为4。
[RegisterCard(typeof(PlumaCardPool))]
public class FullSpeedSlash : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move),      // 基础伤害：升级前3，升级后4
        ModCardVars.Int("BaseHits", 3)          // 基础斩击次数
    };

    public FullSpeedSlash() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        decimal baseDamage = DynamicVars.Damage.BaseValue;
        int baseHits = DynamicVars["BaseHits"].IntValue;

        // 额外段数 = 渐入佳境层数 / 2（向下取整）
        int flowStacks = (int)base.Owner.Creature.GetPowerAmount<FlowState>();
        int extraHits = flowStacks / 2;

        // 基础段数与额外段数合并为一条攻击命令（WithHitCount），
        // 保证活力（VigorPower）等每次攻击消耗的能力对每段伤害都生效，
        // 与旋风斩（Whirlwind）等原版多段牌保持一致。
        await DamageCmd.Attack(baseDamage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitCount(baseHits + extraHits)
            .Execute(choiceContext);
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m); // 伤害 3 → 4
    }
}