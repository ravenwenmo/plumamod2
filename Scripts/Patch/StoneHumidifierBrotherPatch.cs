using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using Pluma.Scripts.Option;

namespace Pluma.Scripts.Patch;

// 石炉加湿器：原版是“每当你在休息处休息时，最大生命 +5”，加在休息者（羽毛笔）身上。
// 羽毛笔在火堆的休息对象是龙舌兰，所以这 +5 改成加在龙舌兰身上，
// 并整条跳过原版效果（羽毛笔自己不再加最大生命）。
// 别的角色（或羽毛笔没有龙舌兰遗物的场合）保持原版行为。
[HarmonyPatch(typeof(StoneHumidifier), nameof(StoneHumidifier.AfterRestSiteHeal))]
public static class StoneHumidifierBrotherPatch
{
    public static bool Prefix(StoneHumidifier __instance, Player player, ref Task __result)
    {
        if (__instance.Owner != player) return true;                 // 只处理遗物持有者自己的休息
        if (!BrotherRestSite.RestsWithBrother(player)) return true;  // 非羽毛笔：按原版走

        // 加多少读遗物自己的 DynamicVars，避免把 5 写死在这里。
        BrotherRestSite.GainMaxHp(player, (int)__instance.DynamicVars.MaxHp.BaseValue);
        __instance.Flash();

        // 原方法是 async Task，跳过方法体必须自己给 __result 赋值，否则 await 会拿到 null。
        __result = Task.CompletedTask;
        return false;
    }
}
