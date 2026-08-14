using Godot;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace Pluma.Scripts;

/// <summary>
/// 局初始化补丁：即使 RitsuLib 的 RunSavedDataPreparingEvent 在多人模式下未触发，
/// 也保证本地玩家在该局的皮肤槽位有值并广播一次（逻辑与事件处理器共用，
/// 由 PlumaSkinSyncAction.EnsureLocalSkinSynced 保证每局幂等）。
/// </summary>
internal sealed class PlumaRunInitSkinPatch : IPatchMethod
{
    public static string PatchId => "pluma_run_init_skin_defaults";

    public static bool IsCritical => false;

    public static string Description => "Ensure local skin slot is populated when a run initializes";

    public static ModPatchTarget[] GetTargets()
    {
        return new ModPatchTarget[1]
        {
            new ModPatchTarget(typeof(RunManager), "InitializeNewRun")
        };
    }

    public static void Postfix(RunManager __instance)
    {
        GD.Print("[pluma] PlumaRunInitSkinPatch.Postfix: RunManager.InitializeNewRun 完成");
        var state = __instance.DebugOnlyGetState();
        if (state == null)
        {
            GD.Print("[pluma] PlumaRunInitSkinPatch: State 为空，跳过");
            return;
        }
        PlumaSkinSyncAction.EnsureLocalSkinSynced(state);
    }
}
