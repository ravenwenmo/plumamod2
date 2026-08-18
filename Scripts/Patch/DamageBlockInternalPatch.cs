using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;

[HarmonyPatch(typeof(Creature))]
public static class DamageBlockInternalPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Creature.DamageBlockInternal))]
    public static void Postfix(
        Creature __instance,
        decimal amount,
        ValueProp props,
        ref decimal __result)
    {
        if (__instance.Player == null)
            return;
        
        if (!__instance.Player.IsBrotherAlive())
            return;

        Creature brother = __instance.Player.Brother();

        if (!(brother.Monster as Brother).DieForYou)
            return;

        decimal remaining =
            amount - __result;


        if (remaining <= 0)
            return;

        decimal petBlocked =
            brother.DamageBlockInternal(
                remaining,
                props
            );

        __result += petBlocked;
    }
}