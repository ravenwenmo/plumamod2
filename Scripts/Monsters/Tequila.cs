
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace Pluma.Scripts.Monsters;

//[RegisterMonster]
public class Tequila : ModMonsterTemplate
{
    public const int INITIAL_HP = 20;
    public override int MinInitialHp => INITIAL_HP;
    public override int MaxInitialHp => INITIAL_HP;

    // 意图1: 强化
    private static int PowerUpAmount => 3;
    // 意图2: 攻击
    private static int BasicDamage => 1;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/tequila/tequila.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 意图1：强化
        var powerUpIntent = new MoveState(
            "POWER_UP", // 状态ID
            PowerUpMove, // 执行函数，或者直接用lambda也可
                         // 以下是可变参数，可以填写任意数量的意图，全部展示
            new BuffIntent()
        );

        // 意图2：攻击
        var attackIntent = new MoveState(
            "ATTACK",
            AttackMove,
            new SingleAttackIntent(BasicDamage)
        );

        // 状态转换，任何意图之后仅接强化意图，攻击意图由玩家控制
        powerUpIntent.FollowUpState = powerUpIntent;
        attackIntent.FollowUpState = powerUpIntent;

        // 添加2个意图，并且初始意图设成 powerUpIntent
        return new MonsterMoveStateMachine([powerUpIntent, attackIntent], powerUpIntent);
    }

    private async Task PowerUpMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 2m, Creature, null);
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd
            .Attack(BasicDamage)
            .FromTequila(this)
            .TargetingAllOpponents(Creature.CombatState)
            .WithAttackerFx(null, AttackSfx)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(null);
    }
}


public static class TequilaAttackCommandExtensions
{
    private static readonly PropertyInfo AttackerProperty =
        AccessTools.Property(
            typeof(AttackCommand),
            nameof(AttackCommand.Attacker)
        );

    private static readonly FieldInfo SourceTypeField =
        AccessTools.Field(
            typeof(AttackCommand),
            "_sourceType"
        );

    private static readonly FieldInfo AttackerAnimNameField =
        AccessTools.Field(
            typeof(AttackCommand),
            "_attackerAnimName"
        );

    private static readonly FieldInfo AttackerAnimDelayField =
        AccessTools.Field(
            typeof(AttackCommand),
            "_attackerAnimDelay"
        );

    public static AttackCommand FromTequila(
        this AttackCommand command,
        MonsterModel monsterModel,
        string? animName = "Attack",
        float animDelay = 0.3f)
    {
        if (monsterModel is not Tequila)
        {
            throw new ArgumentException("Creature is not Tequila.");
        }

        AttackerProperty.SetValue(command, monsterModel.Creature);

        Type sourceType =
            typeof(AttackCommand).GetNestedType(
                "SourceType",
                BindingFlags.NonPublic
            );
        object monster = Enum.Parse(sourceType, "Monster");

        SourceTypeField.SetValue(
            command,
            monster
        );

        AttackerAnimNameField.SetValue(
            command,
            animName
        );

        AttackerAnimDelayField.SetValue(
            command,
            animDelay
        );

        return command;
    }
}

public static class TequilaPlayerExtensions
{
    public static Creature? Tequila(this Player player)
    {
        return player.PlayerCombatState?.GetPet<Tequila>();
    }

    public static bool IsTequilaAlive(this Player player)
    {
        return player.Tequila()?.IsAlive ?? false;
    }

    public static bool IsTequilaMissing(this Player player)
    {
        return !player.IsTequilaAlive();
    }
}