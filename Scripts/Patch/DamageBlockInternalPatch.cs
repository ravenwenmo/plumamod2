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
        
        if (!__instance.Player.IsTequilaAlive())
            return;

        Creature tequila = __instance.Player.Tequila();

        if (!(tequila.Monster as Tequila).DieForYou)
            return;

        decimal remaining =
            amount - __result;


        if (remaining <= 0)
            return;

        decimal petBlocked =
            tequila.DamageBlockInternal(
                remaining,
                props
            );

        __result += petBlocked;
    }
}