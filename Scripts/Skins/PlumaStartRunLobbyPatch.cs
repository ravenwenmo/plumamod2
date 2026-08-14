using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Daily;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Models;

namespace Pluma.Scripts;

/// <summary>
/// 追踪 StartRunLobby 的创建（两个构造函数重载），让皮肤同步代码能找到当前大厅。
/// 目标是 RitsuLib 自身的 RunSavedDataStartRunLobbyCtorPatch 也覆盖的同一批入口。
/// </summary>
internal sealed class PlumaStartRunLobbyCtorPatch : IPatchMethod
{
    public static string PatchId => "pluma_start_run_lobby_ctor_tracking";

    public static bool IsCritical => false;

    public static string Description => "Track start-run lobby creation for skin synchronization";

    public static ModPatchTarget[] GetTargets()
    {
        return new ModPatchTarget[2]
        {
            new ModPatchTarget(typeof(StartRunLobby), ".ctor", new Type[4]
            {
                typeof(GameMode),
                typeof(INetGameService),
                typeof(IStartRunLobbyListener),
                typeof(int)
            }, (MethodType)3),
            new ModPatchTarget(typeof(StartRunLobby), ".ctor", new Type[5]
            {
                typeof(GameMode),
                typeof(INetGameService),
                typeof(IStartRunLobbyListener),
                typeof(TimeServerResult),
                typeof(int)
            }, (MethodType)3)
        };
    }

    public static void Postfix(StartRunLobby __instance)
    {
        GD.Print($"[pluma] PlumaStartRunLobbyCtorPatch: 大厅已创建 netType={__instance.NetService.Type}，注册到追踪表");
        PlumaLobbyRegistry.Track(__instance);
    }
}

/// <summary>
/// 大厅清理时移除追踪，避免拿到已失效的大厅对象。
/// </summary>
internal sealed class PlumaStartRunLobbyCleanUpPatch : IPatchMethod
{
    public static string PatchId => "pluma_start_run_lobby_cleanup_untrack";

    public static bool IsCritical => false;

    public static string Description => "Untrack cleaned-up start-run lobbies for skin synchronization";

    public static ModPatchTarget[] GetTargets()
    {
        return new ModPatchTarget[1]
        {
            new ModPatchTarget(typeof(StartRunLobby), "CleanUp", new Type[2]
            {
                typeof(bool),
                typeof(NetError)
            })
        };
    }

    public static void Postfix(StartRunLobby __instance)
    {
        GD.Print($"[pluma] PlumaStartRunLobbyCleanUpPatch: 大厅已清理 netType={__instance.NetService.Type}，从追踪表移除");
        PlumaLobbyRegistry.Untrack(__instance);
    }
}
