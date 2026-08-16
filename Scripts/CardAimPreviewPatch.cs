using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace Pluma.Scripts;

// Harmony 补丁：追踪卡牌选目标过程，维护 CardAimPreview 状态：
// - 兜割瞄准时 → 敌人血条显示创伤预测（OpenWoundPower 读取）
// - 基酒/鸡尾酒瞄准时 → 卡牌描述按目标阵营切换（SpiritModeDescriptionPatch 读取）
// 仅维护本机 UI 状态，不参与任何游戏逻辑，多人模式下只影响本机显示。
public static class CardAimPreviewPatch
{
    private static bool _applied;
    private static bool _targetingActive;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        var harmony = new Harmony("pluma.card_aim_preview");

        // 选目标开始（Vector2 起点 / Control 起点两个重载）
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), nameof(NTargetManager.StartTargeting),
                new[] { typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) }),
            postfix: new HarmonyMethod(typeof(CardAimPreviewPatch), nameof(StartTargetingPostfix)));
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), nameof(NTargetManager.StartTargeting),
                new[] { typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) }),
            postfix: new HarmonyMethod(typeof(CardAimPreviewPatch), nameof(StartTargetingPostfix)));

        // 选目标结束（出牌成功 / 取消 / 战斗结束）时清空状态
        harmony.Patch(
            AccessTools.Method(typeof(NTargetManager), "FinishTargeting"),
            postfix: new HarmonyMethod(typeof(CardAimPreviewPatch), nameof(FinishTargetingPostfix)));

        // 瞄准/移开目标时更新预览目标（鼠标与手柄瞄准都会经过这里）
        harmony.Patch(
            AccessTools.Method(typeof(NCard), nameof(NCard.SetPreviewTarget)),
            postfix: new HarmonyMethod(typeof(CardAimPreviewPatch), nameof(SetPreviewTargetPostfix)));
    }

    public static void StartTargetingPostfix()
    {
        _targetingActive = true;
    }

    public static void FinishTargetingPostfix()
    {
        _targetingActive = false;
        CardModel? aimedCard = CardAimPreview.CurrentCard;
        CardAimPreview.SetAim(null, null);

        // 取消瞄准后恢复基酒/鸡尾酒描述（右键 SpiritMode 对应的默认描述）。
        // 出牌成功时卡牌已离开手牌，GetCardHolder 返回 null，无需恢复。
        if (aimedCard is ISpiritModeCard &&
            NPlayerHand.Instance?.GetCardHolder(aimedCard) is NHandCardHolder holder)
        {
            holder.UpdateCard();
        }
    }

    public static void SetPreviewTargetPostfix(NCard __instance, Creature? creature)
    {
        // 只在真正的选目标阶段生效，避免出牌队列等其它路径设置预览目标时误触发
        if (!_targetingActive) return;
        // 只追踪与瞄准预览相关的牌：兜割（血条预测）与基酒/鸡尾酒（描述切换）
        if (__instance.Model is not (HelmBreaker or ISpiritModeCard)) return;

        CardAimPreview.SetAim(__instance.Model, creature);

        // 基酒/鸡尾酒：按瞄准目标刷新卡牌描述。
        // 原版 SetPreviewTarget 内的 UpdateVisuals 在我们的状态更新之前执行，
        // 因此这里再刷一次，拿到新状态下的描述。
        if (__instance.Model is ISpiritModeCard)
            __instance.UpdateVisuals(__instance.DisplayingPile, CardPreviewMode.Normal);
    }
}
