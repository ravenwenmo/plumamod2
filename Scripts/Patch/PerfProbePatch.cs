using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Pluma.Scripts.Patch;

// 临时性能探针：定位「进房间黑屏很久」的时间花在哪个阶段。
// 只在日志里多打几行 [PROBE]，不改任何游戏逻辑；定位完成后整个文件删除即可。
//
// 每行格式：[PROBE] <tick> <阶段> heap=<托管堆MB> gc2=<Gen2回收次数>
// 阶段含义：
//   fade_out_begin      屏幕开始变黑
//   exit_rooms_begin    淡出结束、开始卸载旧房间
//   enter_room_begin    开始进入新房间（含 Hook.BeforeRoomEntered）
//   room_enter_begin:x  具体房间的 Enter 开始
//   combat_room_ready_x 战斗房节点创建
//   fade_in_begin       开始淡入（与 fade_out_begin 的差值≈黑屏总时长）
//   frame_gap=xxx       单帧耗时超过 200ms（同步卡顿，主线程被占住）
internal static class PerfProbe
{
    public static void Mark(string what)
    {
        long heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
        GD.Print($"[PROBE] {Time.GetTicksMsec()} {what} heap={heapMb}MB gc2={GC.CollectionCount(2)}");
    }
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.RoomFadeOut))]
internal static class ProbeRoomFadeOut
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("fade_out_begin");
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.RoomFadeIn))]
internal static class ProbeRoomFadeIn
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("fade_in_begin");
}

[HarmonyPatch(typeof(RunManager), "ExitCurrentRooms")]
internal static class ProbeExitCurrentRooms
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("exit_rooms_begin");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoom))]
internal static class ProbeEnterRoom
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("enter_room_begin");
}

[HarmonyPatch(typeof(RunManager), "EnterRoomInternal")]
internal static class ProbeEnterRoomInternal
{
    [HarmonyPrefix]
    public static void Prefix(AbstractRoom room) => PerfProbe.Mark($"enter_room_internal:{room.RoomType}");
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeRoomEntered))]
internal static class ProbeBeforeRoomEntered
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("before_room_entered");
}

[HarmonyPatch(typeof(AbstractRoom), nameof(AbstractRoom.Enter))]
internal static class ProbeAbstractRoomEnter
{
    [HarmonyPrefix]
    public static void Prefix(AbstractRoom __instance) => PerfProbe.Mark($"room_enter_begin:{__instance.RoomType}");
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class ProbeCombatRoomReady
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("combat_room_ready_begin");

    [HarmonyPostfix]
    public static void Postfix() => PerfProbe.Mark("combat_room_ready_end");
}

// NRunMusicController.UpdateTrack 有两个重载（无参 / RoomType），
// 只写 nameof 会让 Harmony 在查找目标方法时抛 AmbiguousMatchException，
// 进而（旧写法里）让整个 PatchAll 中止、后续补丁全部不生效 —— 必须写明参数类型。
[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new Type[0])]
internal static class ProbeUpdateTrack
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("music_update_track");
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateTrack), new[] { typeof(RoomType) })]
internal static class ProbeUpdateTrackForRoom
{
    [HarmonyPrefix]
    public static void Prefix(RoomType roomType) => PerfProbe.Mark($"music_update_track:{roomType}");
}

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.UpdateAmbience))]
internal static class ProbeUpdateAmbience
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("music_update_ambience");
}

[HarmonyPatch(typeof(ActiveScreenContext), nameof(ActiveScreenContext.Update))]
internal static class ProbeScreenContextUpdate
{
    [HarmonyPrefix]
    public static void Prefix() => PerfProbe.Mark("screen_context_update");
}

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class ProbeFrameGap
{
    [HarmonyPrefix]
    public static void Prefix(double delta)
    {
        if (delta > 0.2)
        {
            PerfProbe.Mark($"frame_gap={delta * 1000.0:F0}ms");
        }
    }
}
