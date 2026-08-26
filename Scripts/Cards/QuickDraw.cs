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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;


namespace Pluma.Scripts.Cards;

// 玻利瓦尔拔刀：1费罕见攻击，仅限多人模式。造成基础6+额外10点伤害，但你本回合每打出过一次攻击牌，额外伤害减少5点。
// 升级后额外伤害+4，减少值不变。
[RegisterCard(typeof(PlumaCardPool))]
public class QuickDraw : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害变量：基础6，额外10，每次攻击额外减少5
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(4m),
        new ExtraDamageVar(8m),
        ModCardVars.Int("ReduceAmount", 4),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) =>
        {
            decimal baseDamage = card.DynamicVars.CalculationBase.BaseValue;
            decimal extraDamage = card.DynamicVars.ExtraDamage.BaseValue;
            decimal reduceAmount = card.DynamicVars["ReduceAmount"].BaseValue;
            decimal totalBase = baseDamage + extraDamage;

            // 统计玩家自己本回合已造成的攻击次数
            int myAttackCount = 0;
            var enemies = card.CombatState?.HittableEnemies;
            if (enemies != null)
            {
                myAttackCount = enemies.Sum(enemy =>
                    CombatManager.Instance.History.Entries
                        .OfType<DamageReceivedEntry>()
                        .Count(e =>
                            e.Receiver == enemy &&
                            e.Result.Props.IsPoweredAttack() &&
                            e.HappenedThisTurn(card.CombatState) &&
                            e.Dealer == card.Owner.Creature
                        )
                );
            }

            // 额外伤害随攻击次数减少，最低0
            decimal remainingExtra = Math.Max(0m, extraDamage - reduceAmount * myAttackCount);
            decimal effectiveDamage = baseDamage + remainingExtra;

            if (totalBase <= 0m || effectiveDamage <= 0m)
                return 0m;

            return effectiveDamage / totalBase;
        })
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing, // 切割
    };

    public QuickDraw() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.ExtraDamage.UpgradeValueBy(4m);    // 10 → 14
        // ReduceAmount 保持不变（5）
    }
}