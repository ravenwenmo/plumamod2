using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

// 精准补丁：PersonalHivePower.AfterDamageReceived 仅在伤害来源（dealer）为 null
// 或 dealer 为召唤物（PetOwner 非空，如龙舌兰 Brother、奥斯提 Osty 等宠物）时跳过塞牌，
// 避免以 dealer.Player（对召唤物为 null）生成状态牌引发的空引用崩溃。
// 正常玩家的攻击不受影响，原版塞牌行为完整保留。
// 判断用 PetOwner 而非"dealer.Player.Character is PlumaCharacter"：
// 后者会误伤其他角色的合法塞牌（多人模式中非羽毛笔角色攻击蜂巢敌人时也会被跳过）。
// 注意：原方法为 async Task，Prefix 返回 false 时必须同时给出已完成的 Task 结果，
// 否则调用方 await null 会再次崩溃。
// 通过 Entry.Init 中的 harmony.PatchAll() 自动注册，属本地逻辑，不影响多人同步。
[HarmonyPatch(typeof(PersonalHivePower))]
public static class PersonalHivePower_NullDealerCheckPatch
{
    [HarmonyPatch(nameof(PersonalHivePower.AfterDamageReceived))]
    [HarmonyPrefix]
    public static bool Prefix(
        [HarmonyArgument("dealer")] Creature? dealer,
        ref Task __result)
    {
        // dealer 为空，或 dealer 是召唤物（如龙舌兰），跳过原方法
        if (dealer == null || dealer.PetOwner != null)
        {
            __result = Task.CompletedTask;
            return false;
        }
        return true;
    }
}