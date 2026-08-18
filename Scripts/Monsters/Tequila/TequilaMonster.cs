using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace Pluma.Scripts.Monsters;

[RegisterMonster]
public class Tequila : ModMonsterTemplate
{
    public const int INITIAL_HP = 40;
    public override int MinInitialHp => INITIAL_HP;
    public override int MaxInitialHp => INITIAL_HP;
    public static Vector2 MinOffset => new Vector2(10f, -10f);
	public static Vector2 MaxOffset => new Vector2(50f, -10f);
    public override bool IsHealthBarVisible => true;

    // 意图1: 强化
    private static int PowerUpAmount => 3;
    // 意图2: 攻击
    private static int BasicDamage => 1;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/bro/Bro.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    public override async Task AfterAddedToRoom()
    {
        await CreatureCmd.SetMaxHp(Creature, Math.Max(1, Entry.TequilaStateData.Get(Creature.PetOwner).MaxHp));
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, Entry.TequilaStateData.Get(Creature.PetOwner).Hp));
        await PowerCmd.Apply<TequilaPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);
        GD.Print($"[Tequila] Tequila added to room. CurrentHp: {Creature.CurrentHp}, MaxHp: {Creature.MaxHp}");

        NCreature tequilaNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        tequilaNode?.ToggleIsInteractable(true);
        SetMoveImmediate(GetPowerUpIntent());
    }

    public async Task OrderTequila()
    {
        await Move();
        await SwitchIntent();
    }

    // 仅用作显示，召唤物不主动执行意图
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        GD.Print("[Tequila] Generating move state machine for Tequila");
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
            new BuffIntent()
        );

        return powerUpIntent;
    }

    private MoveState GetAttackIntent()
    {
        var attackIntent = new MoveState(
            "ATTACK",
            AttackMove,
            new SingleAttackIntent(BasicDamage)
        );

        return attackIntent;
    }

    private async Task PowerUpMove(IReadOnlyList<Creature> targets)
    {
        await PowerUpMove();
    }

    public async Task Move()
    {
        if (IntendsToAttack)
        {
            await AttackMove();
        } else
        {
            await PowerUpMove();
        }
    }

    public async Task SwitchIntent()
    {
        if (IntendsToAttack)
        {
            SetMoveImmediate(GetPowerUpIntent());
            await CreatureCmd.TriggerAnim(Creature, "Skill_1_End", 0.3f);
            await PowerCmd.Remove<DieForYouPower>(Creature);
        } else
        {
            SetMoveImmediate(GetAttackIntent());
            await CreatureCmd.TriggerAnim(Creature, "Skill_1_Start", 0.3f);
            await PowerCmd.Apply<DieForYouPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);
        }
    }

    private async Task PowerUpMove()
    {
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, PowerUpAmount, Creature, null);
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await AttackMove();
    }

    private async Task AttackMove()
    {
        GD.Print($"[Tequila] Executing attack move, Side: {Creature.Side}");
        await DamageCmd
            .Attack(BasicDamage)
            .FromTequila(this)
            .TargetingAllOpponents(Creature.CombatState)
            .WithAttackerFx(null, AttackSfx)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
        GD.Print("[Tequila] Generating animator for Tequila");
        AnimState summonAnimState = new AnimState("Start");
		AnimState idleAnimState = new AnimState("Idle", isLooping: true);
		AnimState castAnimState = new AnimState("Skill_1_Start");
        AnimState castIdleAnimState = new AnimState("Skill_1_Idle", isLooping: true);
		AnimState castAttackAnimState = new AnimState("Attack");
        AnimState castEndAnimState = new AnimState("Skill_1_End");
		AnimState deadAnimState = new AnimState("Die");
		AnimState hitAnimState = new AnimState("Die");
        summonAnimState.NextState = idleAnimState;
        idleAnimState.AddBranch("Dead", deadAnimState);
        idleAnimState.AddBranch("Skill_1_Start", castAnimState);
		castAnimState.NextState = castIdleAnimState;
        castIdleAnimState.AddBranch("Hit", hitAnimState);
        castIdleAnimState.AddBranch("Attack", castAttackAnimState);
        castIdleAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castAttackAnimState.AddBranch("Dead", deadAnimState);
		castAttackAnimState.NextState = castIdleAnimState;
        castAttackAnimState.AddBranch("Hit", hitAnimState);
        castAttackAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castEndAnimState.AddBranch("Dead", deadAnimState);
        hitAnimState.NextState = castIdleAnimState;
		castEndAnimState.NextState = idleAnimState;
		CreatureAnimator creatureAnimator = new CreatureAnimator(summonAnimState, controller);
		return creatureAnimator;
	}
}

public class TequilaState
{
    public int Hp { get; set; }

    public int MaxHp { get; set; }

    public TequilaState()
    {
        Hp = Tequila.INITIAL_HP;
        MaxHp = Tequila.INITIAL_HP;
    }
}