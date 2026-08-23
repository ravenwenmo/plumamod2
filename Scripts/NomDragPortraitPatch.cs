using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using Pluma.Scripts.Cards;

namespace Pluma.Scripts;

// Harmony 补丁：从手牌拖拽"嚼"（Nom）时把卡面切换为张口脸（Nom_open），
// 拖拽结束（取消）或打出后恢复原卡面 Nom。
// 纯本地 UI 显示：只维护静态拖拽状态并覆盖 NCard 的 %Portrait 贴图，
// 不触碰任何游戏逻辑、命令与多人同步；恢复依赖框架自带的 UpdateVisuals 刷新。
public static class NomDragPortraitPatch
{
    private static bool _applied;

    private static CardModel? _draggingCard;

    private const string OpenPortraitPath = "res://pluma/images/cards/Nom_open.png";

    private static Texture2D? _openPortrait;

    public static void Apply()
    {
        if (_applied) return;
        _applied = true;

        var harmony = new Harmony("pluma.nom_drag_portrait");

        // 拖拽开始（鼠标流 NMouseCardPlay.Start；键盘快捷键启动的出牌不经过拖拽，跳过切换）
        harmony.Patch(
            AccessTools.Method(typeof(NMouseCardPlay), nameof(NMouseCardPlay.Start)),
            postfix: new HarmonyMethod(typeof(NomDragPortraitPatch), nameof(StartPostfix)));

        // 拖拽结束：取消与打出都会经过这两个方法。
        // 用 prefix 先清状态，之后框架自带的 UpdateVisuals（取消路径在
        // HideTargetingVisuals 中显式调用）会把卡面刷新回原图。
        harmony.Patch(
            AccessTools.Method(typeof(NCardPlay), nameof(NCardPlay.CancelPlayCard)),
            prefix: new HarmonyMethod(typeof(NomDragPortraitPatch), nameof(EndDragPrefix)));
        harmony.Patch(
            AccessTools.Method(typeof(NCardPlay), "TryPlayCard"),
            prefix: new HarmonyMethod(typeof(NomDragPortraitPatch), nameof(EndDragPrefix)));

        // 拖拽期间任何卡面刷新（UpdateVisuals/Reload → UpdatePortrait）都保持张口脸
        harmony.Patch(
            AccessTools.Method(typeof(NCard), "UpdatePortrait"),
            postfix: new HarmonyMethod(typeof(NomDragPortraitPatch), nameof(UpdatePortraitPostfix)));
    }

    public static void StartPostfix(NMouseCardPlay __instance)
    {
        if (__instance.Holder.CardModel is not Nom nom)
        {
            return;
        }

        // 键盘快捷键直接启动的出牌（_skipStartCardDrag）没有拖拽过程，不切换卡面
        bool skipDrag = Traverse.Create(__instance).Field("_skipStartCardDrag").GetValue<bool>();
        if (skipDrag)
        {
            return;
        }

        _draggingCard = nom;
        ApplyOpenPortrait(__instance.Holder.CardNode);
    }

    public static void EndDragPrefix(NCardPlay __instance)
    {
        if (__instance.Holder?.CardModel is not Nom nom || !ReferenceEquals(_draggingCard, nom))
        {
            return;
        }

        _draggingCard = null;

        // 主动刷一次卡面兜底：取消路径 Cleanup 内的 HideTargetingVisuals 本来就会
        // 调 UpdateVisuals，这里再刷只是确保任何结束路径都立刻恢复原卡面。
        __instance.Holder.CardNode?.UpdateVisuals(
            (nom.Pile?.Type).GetValueOrDefault(),
            CardPreviewMode.Normal);
    }

    public static void UpdatePortraitPostfix(NCard __instance)
    {
        if (_draggingCard == null || !ReferenceEquals(_draggingCard, __instance.Model))
        {
            return;
        }

        ApplyOpenPortrait(__instance);
    }

    private static void ApplyOpenPortrait(NCard? cardNode)
    {
        if (cardNode == null || !cardNode.IsInsideTree())
        {
            return;
        }

        if (cardNode.GetNodeOrNull("%Portrait") is not TextureRect portrait)
        {
            return;
        }

        portrait.Texture = _openPortrait ??= ResourceLoader.Load<Texture2D>(
            OpenPortraitPath,
            null,
            ResourceLoader.CacheMode.Reuse);
    }
}
