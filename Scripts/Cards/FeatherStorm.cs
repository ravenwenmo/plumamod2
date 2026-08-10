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

// 羽毛风暴：2费罕见攻击，指定一个目标造成1点伤害并施加2层易伤，然后随机造成1点伤害数次。升级后费用降为1。
[RegisterCard(typeof(PlumaCardPool))]
public class FeatherStorm : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害固定为1，随机攻击次数基础5
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(1m, ValueProp.Move),
        ModCardVars.Int("RandomHits", 5)
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

        var rng = base.Owner.RunState.Rng.CombatCardSelection;
        decimal damage = DynamicVars.Damage.BaseValue;
        int randomHits = DynamicVars["RandomHits"].IntValue;

        // 1. 对指定目标施加2层易伤
        await PowerCmd.Apply<VulnerablePower>(
            choiceContext,
            target,
            2,
            base.Owner.Creature,
            this
        );

        // 2. 对指定目标造成第一下伤害
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        // 3. 随机造成剩余伤害
        for (int i = 0; i < randomHits; i++)
        {
            var randomEnemy = rng.NextItem(enemies);
            if (randomEnemy == null) continue;

            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay)
                .Targeting(randomEnemy)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 2费 → 1费
        // 如需升级增加攻击段数，可取消下面注释：
        // DynamicVars["RandomHits"].UpgradeValueBy(1); // 5 → 6
    }
}