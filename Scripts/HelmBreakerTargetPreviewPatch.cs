using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Pluma.Scripts;

// Harmony 补丁：追踪"兜割"的选目标过程，维护 HelmBreakerTargetPreview 状态，
// 让敌人血条在瞄准时实时显示兜割将触发的创伤预测。
// 仅维护本机 UI 状态，不参与任何游戏逻辑，多人模式下只影响本机显示。
public static class HelmBreakerTargetPreviewPatch
{
    private static bool _applied;
    private static bool _targetingActive;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        var harmony = new Harmony("pluma.helmbreaker_target_preview");

        // 选目标开始（Vector2 起点 / Control 起点两个重载）
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), nameof(NTargetManager.StartTargeting),
                new[] { typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) }),
            postfix: new HarmonyMethod(typeof(HelmBreakerTargetPreviewPatch), nameof(StartTargetingPostfix)));
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), nameof(NTargetManager.StartTargeting),
                new[] { typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) }),
            postfix: new HarmonyMethod(typeof(HelmBreakerTargetPreviewPatch), nameof(StartTargetingPostfix)));

        // 选目标结束（出牌成功 / 取消 / 战斗结束）时清空状态
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), "FinishTargeting"),
            postfix: new HarmonyMethod(typeof(HelmBreakerTargetPreviewPatch), nameof(FinishTargetingPostfix)));

        // 瞄准/移开目标时更新预览目标（鼠标与手柄瞄准都会经过这里）
        harmony.Patch(
            AccessTools.Method(typeof(NCard), nameof(NCard.SetPreviewTarget)),
            postfix: new HarmonyMethod(typeof(HelmBreakerTargetPreviewPatch), nameof(SetPreviewTargetPostfix)));
    }

    public static void StartTargetingPostfix()
    {
        _targetingActive = true;
    }

    public static void FinishTargetingPostfix()
    {
        _targetingActive = false;
        HelmBreakerTargetPreview.SetTarget(null);
    }

    public static void SetPreviewTargetPostfix(NCard __instance, Creature? creature)
    {
        // 只在真正的选目标阶段生效，避免出牌队列等其它路径设置预览目标时误触发
        if (!_targetingActive) return;
        if (__instance.Model is HelmBreaker)
            HelmBreakerTargetPreview.SetTarget(creature);
    }
}
