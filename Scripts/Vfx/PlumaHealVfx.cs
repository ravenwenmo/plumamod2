using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 让"这一次治疗不要播杀戮尖塔原版治疗特效"的作用域开关。
///
/// 原版 <c>CreatureCmd.Heal</c> 会无条件播 <c>vfx/vfx_cross_heal</c>，
/// 羽毛笔的遗物（初始遗物、升华遗物）与「收割者」能力治自己时，
/// 我们希望只播明日方舟官方的治疗特效，所以用一个计数器把原版那一声压掉。
/// 用计数而不是 bool，是为了嵌套/并发调用时安全。
/// </summary>
public static class PlumaHealVfx
{
    private static int _suppressDepth;

    /// <summary>当前是否处于"压制原版治疗特效"的作用域内。</summary>
    public static bool Suppressed => _suppressDepth > 0;

    /// <summary>进入压制作用域（务必配 <see cref="End"/>，建议用 try/finally）。</summary>
    public static void Begin()
    {
        _suppressDepth++;
    }

    /// <summary>退出压制作用域。</summary>
    public static void End()
    {
        if (_suppressDepth > 0) _suppressDepth--;
    }
}

/// <summary>
/// Harmony 补丁：压制作用域内跳过原版治疗特效 <c>vfx/vfx_cross_heal</c>。
///
/// 只拦这一个特效路径，<c>vfx/vfx_heal_osty</c>（奥斯提）以及其它所有 VFX 都不受影响；
/// 治疗数值飘字（NHealNumVfx）、治疗音效也都保持原样，只是不再叠一层原版十字光。
/// 纯本地视觉，不涉及任何同步状态。
/// </summary>
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
public static class PlumaVanillaHealVfxPatch
{
    private const string VanillaHealVfx = "vfx/vfx_cross_heal";

    [HarmonyPrefix]
    public static bool Prefix(string path)
    {
        return !(PlumaHealVfx.Suppressed && path == VanillaHealVfx);
    }
}
