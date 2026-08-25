using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
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

// 羽毛风暴：1费罕见攻击，指定一个目标造成1点伤害并施加2层易伤，然后随机造成1点伤害数次。升级后易伤层数+1。
[RegisterCard(typeof(PlumaCardPool))]
public class FeatherStorm : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害固定为1，随机攻击次数基础5，易伤层数基础1
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(1m, ValueProp.Move),
        ModCardVars.Int("RandomHits", 5),
        ModCardVars.Int("VulnerableAmount", 1)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<VulnerablePower>()
    };

    public FeatherStorm() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        var enemies = CombatState.HittableEnemies;
        if (!enemies.Any()) return;

        // 随机目标使用战斗目标随机流（与 AttackCommand.TargetingRandomOpponents 一致），
        // 保证多人对局中每个客户端的结果一致。
        var rng = base.Owner.RunState.Rng.CombatTargets;
        decimal damage = DynamicVars.Damage.BaseValue;
        int randomHits = DynamicVars["RandomHits"].IntValue;
        decimal vulnerableAmount = DynamicVars["VulnerableAmount"].BaseValue;

        // 指定目标的第一击 + 随机多次攻击必须同属一次攻击（AttackContext）：
        // 活力（VigorPower）在攻击命令结束（AfterAttack）时才消耗层数，
        // 若拆成多次 Execute，第一击后活力就被整段消耗，后续随机攻击不再加成。
        // 原版同款用法见 EchoingSlash / Omnislice。
        await using (var attackContext = await AttackCommand.CreateContextAsync(CombatState, choiceContext, cardPlay))
        {
            // 1. 对指定目标施加易伤
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                target,
                vulnerableAmount,
                base.Owner.Creature,
                this
            );

            // 2. 对指定目标造成第一下伤害（保留 Execute 默认的攻击动画）
            await CreatureCmd.TriggerAnim(base.Owner.Creature, "Attack", base.Owner.Character.AttackAnimDelay);
            attackContext.AddHit(await CreatureCmd.Damage(
                choiceContext, target, damage, ValueProp.Move, this, cardPlay));

            // 3. 随机造成剩余伤害
            for (int i = 0; i < randomHits; i++)
            {
                var randomEnemy = rng.NextItem(enemies);
                if (randomEnemy == null || !randomEnemy.IsAlive) continue;

                await CreatureCmd.TriggerAnim(base.Owner.Creature, "Attack", base.Owner.Character.AttackAnimDelay);
                attackContext.AddHit(await CreatureCmd.Damage(
                    choiceContext, randomEnemy, damage, ValueProp.Move, this, cardPlay));
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后易伤层数 +1（2 → 3）
        DynamicVars["VulnerableAmount"].UpgradeValueBy(1m);
    }
}