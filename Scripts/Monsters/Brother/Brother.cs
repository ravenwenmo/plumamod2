using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace Pluma.Scripts.Monsters;

// 龙舌兰（Brother）：与玩家并肩作战的召唤物，意图在"强化循环"与"攻击循环"之间切换。
// 强化循环：每回合开始时获得1点力量，力量达到 STRENGTH_THRESHOLD 后切换为攻击循环。
// 攻击循环：造成群体伤害（段数受 BrotherExtraHitsPower 层数加成），持续 ATTACK_INTENT_TURNS
// 回合后清空力量并切回强化循环。
// 力量、意图、剩余攻击回合与生命值跨战斗持久化（见 BrotherStateData）。
[RegisterMonster]
public class Brother : ModMonsterTemplate
{
    // 基础生命上限
    public const int INITIAL_HP = 66;
    // 强化循环意图每回合获得的特性层数
    private const int TraitPerTurn = 25;
    // 特性达到该值后切换为攻击循环意图
    public const int TraitThreshold = 200;
    // 攻击循环意图持续的回合数（变量，可调整）
    public const int ATTACK_INTENT_TURNS = 3;
    // 攻击基础段数（额外段数由 BrotherExtraHitsPower 层数提供）
    public const int ATTACK_BASE_HITS = 1;
    // 每段基础伤害
    public const int BasicDamage = 4;

    public override int MinInitialHp => INITIAL_HP;
    public override int MaxInitialHp => INITIAL_HP;
    public static Vector2 MinOffset => new Vector2(10f, -10f);
	public static Vector2 MaxOffset => new Vector2(50f, -10f);
    public override bool IsHealthBarVisible => true;

    // 攻击循环意图期间为玩家吸收未格挡的攻击伤害（见 BrotherPower / DamageBlockInternalPatch）
    public bool DieForYou { get; set; } = false;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/bro/Bro.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    public override async Task AfterAddedToRoom()
    {
        await CreatureCmd.SetMaxHp(Creature, Math.Max(1, BrotherStateData.GetMaxHp(Creature.PetOwner)));
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, BrotherStateData.GetHp(Creature.PetOwner)));
        await PowerCmd.Apply<BrotherPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);
        await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetAttackTurnsRemaining(Creature.PetOwner), null, null);
        // 额外增加一个被动
        await PowerCmd.Apply<MarkerRecoveryPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            10m,
            null,
            null
        );

        // 恢复特性层数（替换原来的力量恢复）
        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetTrait(Creature.PetOwner), null, null);

        NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        brotherNode?.ToggleIsInteractable(true);
    }

    // 测试牌重复打出时：回满生命值并切换为攻击循环意图
    public async Task OrderBrother()
    {
        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        await SwitchToAttackIntent();
    }

    // 回合开始（由 BrotherSupportPower 驱动）：
    // 强化循环期间获得力量。力量值的同步与达到阈值的切换由
    // BrotherPower.AfterPowerAmountChanged 实时完成（控制台加力量也能即时触发），
    // 这里无需再手动检查阈值。
    public async Task PlayerTurnStart()
    {
        if (IntendsToAttack)
        {
            return; // 攻击循环期间回合开始无动作
        }

        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, TraitPerTurn, Creature, null);
        GD.Print($"[Brother] Power-up turn start: trait {Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}");
    }


    // 回合结束（由 BrotherSupportPower 驱动）：
    // 攻击循环期间执行群体攻击，并结算剩余攻击回合
  
    public async Task TakeTurn(PlayerChoiceContext choiceContext)
    {
        if (!IntendsToAttack)
        {
            return; // 强化循环期间回合结束无动作
        }

        int hits = ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>();
        decimal damage = BasicDamage;
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0.3f);
        IReadOnlyList<Creature> targets = Creature.CombatState.GetOpponentsOf(Creature);
        // 消耗一层剑走偏锋执行攻击
        await PowerCmd.Decrement(Creature.GetPower<BrotherAttackTurnsPower>());
        for (int i = 0; i < hits; i++)
        {
            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, Creature);
        }
        if (Creature.GetPowerAmount<BrotherAttackTurnsPower>() <= 0)
        {
            // 攻击循环结束，清空特性并切回强化循环
            await PowerCmd.Remove<TraitPower>(Creature);
            await SwitchToPowerUpIntent();
        }
    }

    // 切换为攻击循环意图
    public async Task SwitchToAttackIntent()
    {
        if (IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetAttackIntent());
        await CreatureCmd.TriggerAnim(Creature, "Skill_1_Start", 0.3f);
        DieForYou = true;

        // 双重身份：进入攻击循环后刷新冷却
        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            doubleIdentity.RefreshCooldown();
        }
    }

    // 切换为强化循环意图
    public async Task SwitchToPowerUpIntent()
    {
        if (!IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetPowerUpIntent());
        await CreatureCmd.TriggerAnim(Creature, "Skill_1_End", 0.3f);
        DieForYou = false;

        // 双重身份：进入强化循环时触发一次
        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            await doubleIdentity.TriggerOnPowerUpCycle();
        }
    }


    public async Task TriggerWhenGainTrait()
    {
        if (Creature.HasPower<BrotherAttackTurnsPower>())
        {
            // 已经处于攻击意图
            return;
        }
        if (Creature.GetPowerAmount<TraitPower>() >= TraitThreshold)
        {
            // 给予攻击意图
            await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, ATTACK_INTENT_TURNS, Creature, null);
        }
    }
    
    // 仅用作显示，召唤物不主动执行意图（实际行动由 BrotherSupportPower 在回合钩子中驱动）
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        GD.Print("[Brother] Generating move state machine for Brother");
        // 意图1：强化
        var powerUpIntent = GetPowerUpIntent();

        // 意图2：攻击
        var attackIntent = GetAttackIntent();

        // 状态转换，任何意图之后仅接强化意图，攻击意图由玩家控制
        powerUpIntent.FollowUpState = powerUpIntent;
        attackIntent.FollowUpState = powerUpIntent;

        // 添加2个意图，并且初始意图设成 powerUpIntent
        return new MonsterMoveStateMachine([powerUpIntent, attackIntent], powerUpIntent);
    }

    private MoveState GetPowerUpIntent()
    {
        var powerUpIntent = new MoveState(
            "POWER_UP",
            PowerUpMove,
            new BrotherBuffIntent()
        );

        return powerUpIntent;
    }

    private MoveState GetAttackIntent()
    {
        var attackIntent = new MoveState(
            "ATTACK",
            AttackMove,
            new BrotherAttackIntent(BasicDamage, () => ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>())
        );

        return attackIntent;
    }

    private async Task PowerUpMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
        GD.Print("[Brother] Generating animator for Brother");
        AnimState summonAnimState = new AnimState("Start");
		AnimState idleAnimState = new AnimState("Idle", isLooping: true);
		AnimState castAnimState = new AnimState("Skill_1_Start");
        AnimState castIdleAnimState = new AnimState("Skill_1_Idle", isLooping: true);
		AnimState castAttackAnimState = new AnimState("Attack");
        AnimState castEndAnimState = new AnimState("Skill_1_End");
		AnimState deadAnimState = new AnimState("Die");
        summonAnimState.NextState = idleAnimState;
        summonAnimState.AddBranch("Skill_1_Start", castAnimState);
        idleAnimState.AddBranch("Dead", deadAnimState);
        idleAnimState.AddBranch("Skill_1_Start", castAnimState);
		castAnimState.NextState = castIdleAnimState;
        castIdleAnimState.AddBranch("Attack", castAttackAnimState);
        castIdleAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castIdleAnimState.AddBranch("Dead", deadAnimState);
        castAttackAnimState.AddBranch("Dead", deadAnimState);
		castAttackAnimState.NextState = castIdleAnimState;
        castAttackAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castEndAnimState.AddBranch("Dead", deadAnimState);
		castEndAnimState.NextState = idleAnimState;
		CreatureAnimator creatureAnimator = new CreatureAnimator(summonAnimState, controller);
		return creatureAnimator;
	}
}