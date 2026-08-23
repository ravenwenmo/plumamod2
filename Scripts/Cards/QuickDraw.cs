using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;

using MegaCrit.Sts2.Core.Combat.History.Entries;

namespace Pluma.Scripts.Cards;

// 玻利瓦尔拔刀：1费罕见攻击，仅限多人模式。对全体敌人造成伤害。
// 伤害 = (基础6 + 额外10) - 你自己本回合已攻击次数 × ReduceAmount(2)。升级后额外+4，ReduceAmount+1。
[RegisterCard(typeof(PlumaCardPool))]
public class QuickDraw : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;
    

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害变量
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),      // 基础伤害
        new ExtraDamageVar(10m),                 // 额外伤害
        ModCardVars.Int("ReduceAmount", 2)       // 每攻击一次减少的伤害量
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing, // 本能
    };
    
    public QuickDraw() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 计算玩家自己本回合已造成的攻击次数
        int myAttackCount = 0;
        var enemies = CombatState.HittableEnemies;
        if (enemies != null)
        {
            myAttackCount = enemies.Sum(enemy =>
                CombatManager.Instance.History.Entries
                    .OfType<DamageReceivedEntry>()
                    .Count(e =>
                        e.Receiver == enemy &&
                        e.Result.Props.IsPoweredAttack() &&
                        e.HappenedThisTurn(CombatState) &&
                        e.Dealer == base.Owner.Creature  // 只统计自己
                    )
            );
        }

        // 2. 计算实际伤害 = 基础 + 额外 - 攻击次数 * ReduceAmount
        decimal baseDamage = DynamicVars.Damage.BaseValue + DynamicVars.ExtraDamage.BaseValue;
        decimal actualDamage = Math.Max(0m, baseDamage - myAttackCount * DynamicVars["ReduceAmount"].BaseValue);

        // 3. 对全体敌人造成伤害
        await DamageCmd.Attack(actualDamage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(4m);    // 10 → 14
        DynamicVars["ReduceAmount"].UpgradeValueBy(1m); // 2 → 3
    }
}