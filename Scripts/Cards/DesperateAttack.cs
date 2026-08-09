using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 舍身攻击：1费稀有攻击牌，获得1层“无法获得格挡”，造成2点伤害7次。升级后造成10次。
[RegisterCard(typeof(PlumaCardPool))]
public class DesperateAttack : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 悬浮提示：显示 NoBlockPower 的解释
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<NoBlockPower>()
    };

    // 动态变量：基础伤害 2，攻击次数 7（升级后+3）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(2m, ValueProp.Move),
        ModCardVars.Int("Hits", 7)
    };

    public DesperateAttack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 移除自己所有格挡
        await CreatureCmd.LoseBlock(choiceContext, Owner.Creature, Owner.Creature.Block, Owner.Creature);
        // 先给自己施加“无法获得格挡”
        await PowerCmd.Apply<NoBlockPower>(
            choiceContext,
            base.Owner.Creature,
            2,
            base.Owner.Creature,
            this
        );

        // 多次攻击目标
        int hits = DynamicVars["Hits"].IntValue;
        decimal damagePerHit = DynamicVars.Damage.BaseValue;

        for (int i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(damagePerHit)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target!)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(3); // 7 → 10
    }
}