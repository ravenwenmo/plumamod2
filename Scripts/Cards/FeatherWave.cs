using System.Collections.Generic;
using System.Linq;
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

// 羽浪：1费稀有攻击，获得1层渐入佳境，对所有敌人造成伤害3次，并对生命最高的敌人施加创伤（等于渐入佳境层数，升级后+1）。
[RegisterCard(typeof(PlumaCardPool))]
public class FeatherWave : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害变量：基础4，升级后+2 → 6
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(4m, ValueProp.Move)
    };

    // 悬浮提示：创伤能力
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };

    public FeatherWave() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 获得1层渐入佳境（先获得，使后续伤害吃加成）
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );

        // 2. 对所有敌人造成3次伤害
        decimal damage = DynamicVars.Damage.BaseValue;
        for (int i = 0; i < 3; i++)
        {
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .Execute(choiceContext);
        }

        // 3. 获取当前渐入佳境层数（包含刚刚获得的1层）
        int flowStacks = (int)base.Owner.Creature.GetPowerAmount<FlowState>();

        // 4. 计算创伤层数：基础 = 渐入佳境层数，升级后额外+1
        int woundStacks = flowStacks + (base.IsUpgraded ? 1 : 0);

        // 5. 找到生命值最高的敌人
        var targetEnemy = CombatState.HittableEnemies
            .OrderByDescending(e => e.CurrentHp)
            .FirstOrDefault();

        if (targetEnemy != null && woundStacks > 0)
        {
            await PowerCmd.Apply<OpenWoundPower>(
                choiceContext,
                targetEnemy,
                woundStacks,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 4 → 6
    }
}