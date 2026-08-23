using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Pluma.Scripts;

// Harmony 补丁：羽毛笔默认攻击动画（Attack 触发器 → "attack" 状态）按渐入佳境层数加速。
// 速度 = 1 + 3% × min(层数, 12)，12 层封顶。
//
// 速度只写在当前攻击动画的 TrackEntry 上：Spine 每次 SetAnimation/AddAnimation 都会创建
// 新的轨道条目（默认速度 1.0），攻击播完接回的待机等动画是新条目，不会继承加速，
// 因此无需额外恢复（参考原版 CreatureAnimator.OffsetLoopingAnimation 对条目级速度的使用）。
//
// 只匹配 trigger == "Attack"：Skill_2 系列、Cast、Hit、Idle 等触发器不受影响。
// 纯本地视觉，不读写任何同步数据，多人模式下各机器只影响各自的画面。
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class PlumaAttackAnimSpeedPatch
{
    private const float SpeedPerStack = 0.03f;
    private const int MaxStacks = 12;

    [HarmonyPostfix]
    public static void Postfix(NCreature __instance, string trigger)
    {
        // 只处理默认攻击动画
        if (trigger != CreatureAnimator.attackTrigger) return;

        // 只加速羽毛笔玩家角色本体（宠物与其它角色不受影响）
        var entity = __instance.Entity;
        if (entity?.Player?.Character is not PlumaCharacter) return;

        int stacks = entity.GetPowerAmount<FlowState>();
        if (stacks <= 0) return;

        // 防御性检查：确认动画器确实切到了 attack 状态。
        // 若骨骼缺少该动画，原版会保留当前动画不切换，此时不应改动正在播放的条目。
        using var track = __instance.SpineAnimation.GetCurrentTrack();
        if (track == null || track.GetAnimationName() != AnimState.attackAnim) return;

        float speed = 1f + SpeedPerStack * Math.Min(stacks, MaxStacks);
        track.SetTimeScale(speed);
    }
}
