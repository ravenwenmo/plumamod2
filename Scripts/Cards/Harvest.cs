using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 收割：对一名敌人造成10点伤害，若其生命值低于一半则伤害提升至1.5倍。获得1层渐入佳境。
[RegisterCard(typeof(PlumaCardPool))]
public class Harvest : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;   // 改为单体目标
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 动态变量：基础伤害10，升级+3
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(10m, ValueProp.Move)
    };

    // 悬浮提示：渐入佳境
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    public Harvest() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        decimal finalDamage = DynamicVars.Damage.BaseValue;

        // 若目标当前生命值低于最大生命值一半，伤害提升50%
        if (target.CurrentHp < target.MaxHp / 2m)
        {
            finalDamage = DynamicVars.Damage.BaseValue * 1.5m;
        }

        await DamageCmd.Attack(finalDamage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        // 获得1层渐入佳境
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
        
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);   // 10 → 13
    }
}