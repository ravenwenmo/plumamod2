using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2RitsuLib.Patching.Models;

namespace Pluma.Scripts;

/// <summary>
/// 在 Creature.CreateVisuals 调用期间记录"正在为哪个玩家创建战斗模型"。
///
/// 背景：游戏创建模型的调用链是 Creature.CreateVisuals() → Player.Character.CreateVisuals()，
/// 而 RitsuLib 的 IModCreatureVisualsFactory.TryCreateCreatureVisuals() 不携带玩家参数，
/// 只有 Creature 这一层知道当前是哪个玩家。因此这里把玩家压入环境上下文，
/// 让 PlumaCharacter.TryCreateCreatureVisuals 能按该玩家读取皮肤同步槽位。
/// </summary>
internal sealed class PlumaCreatureVisualsContextPatch : IPatchMethod
{
    public static string PatchId => "pluma_creature_visuals_player_context";

    public static bool IsCritical => false;

    public static string Description => "Provide per-player context for skin-aware creature visuals";

    public static ModPatchTarget[] GetTargets()
    {
        return new ModPatchTarget[1]
        {
            new ModPatchTarget(typeof(Creature), "CreateVisuals")
        };
    }

    public static void Prefix(Creature __instance)
    {
        if (__instance.Player?.Character is PlumaCharacter)
            PlumaSkins.PushVisualsPlayer(__instance.Player);
    }

    public static void Postfix(Creature __instance)
    {
        if (__instance.Player?.Character is PlumaCharacter)
            PlumaSkins.PopVisualsPlayer();
    }
}
