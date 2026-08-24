using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat; // 可能不需要，若用 CombatManager
using MegaCrit.Sts2.Core.Combat;      // 根据实际命名空间调整

namespace Pluma.Scripts;

// Harmony 补丁：三形态卡牌（基酒/鸡尾酒）的 Description 按当前 SpiritMode 返回独立描述；
// 瞄准某个单位时则按瞄准目标阵营返回对应分支描述（只影响本地显示，不改变实际效果）。
// 原版 CardModel.Description 是非虚属性，无法直接重写，因此用后置补丁替换返回值。
// 原版 GetDescriptionForPile 会继续为替换后的 LocString 附加 DynamicVars（{Damage:diff()} 等），
// 且 NCard.UpdateVisuals 每次刷新都会重新获取描述，右键切换后调用 holder.UpdateCard() 即可立即生效。
public static class SpiritModeDescriptionPatch
{
    private static bool _applied;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        var harmony = new Harmony("pluma.spiritmode_description");
        var getter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        harmony.Patch(getter,
            postfix: new HarmonyMethod(typeof(SpiritModeDescriptionPatch), nameof(Postfix)));
    }

    public static void Postfix(CardModel __instance, ref LocString __result)
    {
        
        // 只在战斗进行中应用动态描述；非战斗状态保留默认通用描述
        if (CombatManager.Instance == null || CombatManager.Instance.IsOverOrEnding)
            return;
        
        if (__instance is ISpiritModeCard spiritCard)
        {
            // 瞄准预览优先：指向某个单位时按目标阵营显示对应分支描述；
            // 否则保持右键切换 SpiritMode 的描述。
            SpiritTargetBranch? aimBranch = CardAimPreview.GetAimBranchFor(__instance);
            __result = aimBranch.HasValue
                ? spiritCard.GetSpiritDescriptionFor(aimBranch.Value)
                : spiritCard.SpiritModeDescription;
        }
    }
}
