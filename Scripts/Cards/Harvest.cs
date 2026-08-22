using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 收割：X费罕见攻击牌，切割。造成基础伤害X次，对全体敌人。对低于半血的敌人额外造成伤害（升级前+40%，升级后+50%）。
// 动画：Skill_2_Start → 每段攻击 Skill_2_Attack → Skill_2_End。
[RegisterCard(typeof(PlumaCardPool))]
public class Harvest : ModCardTemplate
{
    private const int energyCost = 0; // X费牌，实际费用由 HasEnergyCostX 控制
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    protected override bool HasEnergyCostX => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Move),   // 基础伤害：升级前 5，升级后 7
        ModCardVars.Int("BonusPercent", 40)  // 额外伤害百分比：升级前 40，升级后 50
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing // 切割
    };

    public Harvest()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();
        if (x <= 0) return;

        // 施加临时加成 Power：层数 = 额外伤害百分比
        await PowerCmd.Apply<HarvestTempPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["BonusPercent"].BaseValue,
            base.Owner.Creature,
            this
        );

        var creature = base.Owner.Creature;
        var creatureNode = creature.GetCreatureNode();

        // 动画速度倍率：S = min(10, (1 + 3%×渐入佳境层数) / 0.65)，升级后分母 0.5。
        // 纯本地视觉，多人模式下只影响各自机器的动画节奏，不影响同步数据。
        int flowStacks = creature.GetPowerAmount<FlowState>();
        float speed = MathF.Min(10f, (float)((1m + 0.03m * flowStacks) / (base.IsUpgraded ? 0.5m : 0.65m)));
        creatureNode?.SpineAnimation.SetTimeScale(speed);

        try
        {
            // 打出前动画：播完再开始攻击
            await PlaySkill2AnimAndWait(creature, creatureNode, "Skill_2_Start", speed);

            // 每段循环：Skill_2_Attack 播完后结算该段伤害
            for (int i = 0; i < x; i++)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .TargetingAllOpponents(base.CombatState)
                    .WithAttackerAnim("Skill_2_Attack", 0f)
                    .WithAttackerFx(sfx: base.Owner.Character.AttackSfx)
                    .AfterAttackerAnim(async () =>
                    {
                        // 动画已同步触发，读当前动画名义时长并按速度倍率等待播完
                        float length = creatureNode?.GetCurrentAnimationLength() ?? 0f;
                        await WaitAnimLength(length, speed);
                    })
                    //.WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
            }

            // 结束动画：播完再收尾
            await PlaySkill2AnimAndWait(creature, creatureNode, "Skill_2_End", speed);
        }
        finally
        {
            // 恢复动画速度，并移除临时加成 Power
            creatureNode?.SpineAnimation.SetTimeScale(1f);
            await PowerCmd.Remove<HarvestTempPower>(base.Owner.Creature);
        }
    }

    // 触发一次 Skill_2 动画并等待其按当前速度倍率播放完成
    private async Task PlaySkill2AnimAndWait(Creature creature, NCreature? creatureNode, string animName, float speed)
    {
        await CreatureCmd.TriggerAnim(creature, animName, 0f); // 只触发动画，不固定等待
        float length = creatureNode?.GetCurrentAnimationLength() ?? 0f;
        await WaitAnimLength(length, speed);
    }

    // 按名义时长与速度倍率等待动画播完；快/标准模式语义与原版 TriggerAnim 一致
    private async Task WaitAnimLength(float length, float speed)
    {
        if (length <= 0f || speed <= 0f) return;
        float wait = length / speed;
        await Cmd.CustomScaledWait(MathF.Min(wait * 0.5f, 0.25f), wait);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);            // 5 → 7
        DynamicVars["BonusPercent"].UpgradeValueBy(10m);  // 40 → 50
    }
}


/*
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
*/