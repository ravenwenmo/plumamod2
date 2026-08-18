using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Pluma.Scripts.Monsters;

public static class BrotherAttackCommandExtensions
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

    public static AttackCommand FromBrother(
        this AttackCommand command,
        MonsterModel monsterModel,
        string? animName = "Attack",
        float animDelay = 0.3f)
    {
        if (monsterModel is not Brother)
        {
            throw new ArgumentException("Creature is not Brother.");
        }

        AttackerProperty.SetValue(command, monsterModel.Creature);

        Type sourceType =
            typeof(AttackCommand).GetNestedType(
                "SourceType",
                BindingFlags.NonPublic
            );
        object card = Enum.Parse(sourceType, "Card");

        SourceTypeField.SetValue(
            command,
            card
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

public static class BrotherPlayerExtensions
{
    public static Creature? Brother(this Player player)
    {
        return player.PlayerCombatState?.GetPet<Brother>();
    }

    public static bool IsBrotherAlive(this Player player)
    {
        return player.Brother()?.IsAlive ?? false;
    }

    public static bool IsBrotherMissing(this Player player)
    {
        return !player.IsBrotherAlive();
    }
}