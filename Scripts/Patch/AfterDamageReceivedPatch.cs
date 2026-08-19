using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using Pluma.Scripts.Monsters;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageReceived))]
public static class AfterDamageReceivedPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref Creature? dealer)
    {
        if (dealer?.Monster is Brother)
        {
            dealer = null;
        }
    }
}