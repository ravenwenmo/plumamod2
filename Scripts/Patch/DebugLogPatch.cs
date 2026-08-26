using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Pluma.Scripts.Patch;

[HarmonyPatch(
    typeof(CombatManager),
    nameof(CombatManager.AfterCreatureAdded),
    [
        typeof(Creature)
    ]
)]
public static class AfterCreatureAddedLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(CombatManager __instance, Creature creature)
    {
        GD.Print($"[AfterCreatureAddedLogPatch] Creature added to room: {creature.Name}");
    }
}

[HarmonyPatch(
    typeof(NCreature),
    nameof(NCreature.ToggleIsInteractable)
)]
public static class ToggleIsInteractableLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(NCreature __instance, bool on)
    {
        GD.Print($"[ToggleIsInteractableLogPatch] ToggleIsInteractable called for creature: {__instance}, On: {on}");
    }
}

[HarmonyPatch(
    typeof(NCreatureStateDisplay),
    "ShowNameplate"
)]
public static class ShowNameplateLogPatch
{
    private static readonly FieldInfo CreatureField =
        AccessTools.Field(
            typeof(NCreatureStateDisplay),
            "_creature"
        );

    private static readonly FieldInfo NameplateField =
        AccessTools.Field(
            typeof(NCreatureStateDisplay),
            "_nameplateContainer"
        );

    [HarmonyPrefix]
    public static void Prefix(NCreatureStateDisplay __instance)
    {
        GD.Print($"[ShowNameplateLogPatch] _stateDisplay.Visible: {__instance.Visible}");
        GD.Print($"[ShowNameplateLogPatch] ShowNameplate called for creature: {CreatureField.GetValue(__instance) as Creature}, Nameplate.Visible: {(NameplateField.GetValue(__instance) as Control)?.Visible}");
    }
}