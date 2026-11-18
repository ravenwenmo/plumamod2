// 调试用补丁：默认不编入。
// 之前在正式游玩时会往 godot.log 里刷大量 [ShowNameplateLogPatch] 行（单局 700+ 行，
// 占日志近三成），既拖慢日志写入也淹没真正有用的信息，所以用编译开关关掉。
// 需要排查名牌/可交互/生物加入问题时，在 pluma.csproj 的 DefineConstants 里加
// PLUMA_DEBUG_PATCHES 再构建即可。
#if PLUMA_DEBUG_PATCHES

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

// 0.113 起 NCreature.ToggleIsInteractable(bool) 改名为 SetIsInteractable(bool on, bool changeVisibilityImmediately)
[HarmonyPatch(
    typeof(NCreature),
    nameof(NCreature.SetIsInteractable)
)]
public static class SetIsInteractableLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(NCreature __instance, bool on)
    {
        GD.Print($"[SetIsInteractableLogPatch] SetIsInteractable called for creature: {__instance}, On: {on}");
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

#endif
