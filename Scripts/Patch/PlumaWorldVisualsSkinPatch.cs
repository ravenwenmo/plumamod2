using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Random;

namespace Pluma.Scripts.Patch;

/// <summary>
/// 商店/篝火世界场景按玩家皮肤替换"空手版"骨架并播放 Relax / Sit 动画。
///
/// 背景：CharacterModel 的 MerchantAnimPath / RestSiteAnimPath 是角色级别的静态路径，
/// 无法按玩家皮肤返回不同的 .tscn。因此 AssetProfile 里先填默认皮肤（ori）的场景，
/// 场景被实例化后，这里再根据每个玩家的皮肤同步槽位把 SpineSprite 的 skeleton_data_res
/// 换成对应皮肤的空手版骨架（ori/sum 两套骨架的动画名一致：Relax=商店，Sit=篝火）。
/// </summary>
internal static class PlumaWorldVisualsSkinPatch
{
    // 商店：RitsuLib 会把我们的场景根节点（Node2D）包成 NMerchantCharacter 的子节点，
    // SpineSprite 在它下面一层，所以递归查找第一个 SpineSprite。
    internal static void ApplySkinToMerchant(NMerchantCharacter visual, Player player)
    {
        Node? spine = FindSpineSprite(visual);
        if (spine == null)
        {
            return;
        }
        ApplySkeletonAndPlay(visual, spine, player, "Relax");
    }

    // 递归查找第一个 SpineSprite 子节点（世界场景里只有一个）
    internal static Node? FindSpineSprite(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child.GetClass() == "SpineSprite")
            {
                return child;
            }
            Node? nested = FindSpineSprite(child);
            if (nested != null)
            {
                return nested;
            }
        }
        return null;
    }

    // 篝火：NRestSiteCharacter 的直接子节点里可能有多个 SpineSprite（原版结构），逐个替换。
    // 基础 _Ready 的 act 动画和 FlipX 已被 PlumaRestSiteSpineNodesPatch 屏蔽（空列表），
    // 所以这里自己按玩家编号补上翻转（与 NRestSiteRoom._Ready 的 i % 2 == 1 规则一致）。
    internal static void ApplySkinToRestSite(NRestSiteCharacter character, int characterIndex)
    {
        if (character.Player == null)
        {
            return;
        }
        bool flip = characterIndex % 2 == 1;
        foreach (Node2D spine in character.GetChildren().OfType<Node2D>().Where(n => n.GetClass() == "SpineSprite"))
        {
            ApplySkeletonAndPlay(character, spine, character.Player, "Sit");
            if (flip)
            {
                spine.Scale = new Vector2(-Mathf.Abs(spine.Scale.X), spine.Scale.Y);
                spine.Position = new Vector2(-spine.Position.X, spine.Position.Y);
            }
        }
    }

    private static void ApplySkeletonAndPlay(Node host, Node spineNode, Player player, string animName)
    {
        string skeletonPath = PlumaSkins.GetCurrentSkin(player).WorldSkeletonDataPath;
        Resource? skeleton = ResourceLoader.Load(skeletonPath);
        if (skeleton == null)
        {
            GD.PrintErr($"[pluma] 世界场景骨架资源缺失: {skeletonPath}");
            return;
        }

        spineNode.Set("skeleton_data_res", skeleton);
        MegaSprite sprite = new(spineNode);
        TaskHelper.RunSafely(PlayWhenReady(host, sprite, animName));
    }

    /// <summary>
    /// 替换 skeleton_data_res 后骨架是异步重建的，等新骨架就绪（且包含目标动画）再播放。
    /// </summary>
    private static async Task PlayWhenReady(Node host, MegaSprite sprite, string animName)
    {
        try
        {
            for (int i = 0; i < 120; i++)
            {
                if (GodotObject.IsInstanceValid(sprite.BoundObject)
                    && sprite.IsAnimationStateReady()
                    && sprite.HasAnimation(animName))
                {
                    MegaAnimationState state = sprite.GetAnimationState();
                    state.SetAnimation(animName, loop: true);
                    // 与原版商店/篝火一致：循环动画随机起点，避免多人时动作完全同步
                    using (MegaTrackEntry? entry = state.GetCurrent(0))
                    {
                        entry?.SetTrackTime(entry.GetAnimationEnd() * Rng.Chaotic.NextFloat());
                    }
                    return;
                }
                await host.AwaitProcessFrame();
            }
            GD.PrintErr($"[pluma] 世界场景骨架动画未就绪: {animName}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[pluma] 世界场景动画播放失败: {e}");
        }
    }
}

/// <summary>
/// 商店角色 _Ready 时直接播放空手版骨架的 Relax。
///
/// 原版 NMerchantCharacter._Ready 固定播放 relaxed_loop，而我们的世界骨架没有这个名字
/// （基建动画导出名是 Relax）。这里识别出羽毛笔的商店场景，跳过原版逻辑，
/// 等骨架就绪后播放 Relax，避免 "Animation not found: relaxed_loop" 报错和初始摆姿势帧。
/// 皮肤骨架的替换仍由 PlumaMerchantSkinPatch 在房间创建完成后处理。
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), "_Ready")]
internal static class PlumaMerchantReadyPatch
{
    public static bool Prefix(NMerchantCharacter __instance)
    {
        Node? child = PlumaWorldVisualsSkinPatch.FindSpineSprite(__instance);
        if (child == null)
        {
            return true;
        }
        Resource? res = child.Get("skeleton_data_res").AsGodotObject() as Resource;
        if (res == null || !res.ResourcePath.Contains("pluma/images/spineAni"))
        {
            return true;
        }

        MegaSprite sprite = new(child);
        __instance.RunWhenSpineReady(sprite, state => state.SetAnimation("Relax", loop: true));
        return false;
    }
}

/// <summary>
/// 商店房间创建完角色视觉后，把每个羽毛笔玩家对应的 SpineSprite 换成其皮肤的骨架并播放 Relax。
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class PlumaMerchantSkinPatch
{
    private static readonly AccessTools.FieldRef<NMerchantRoom, List<Player>> PlayersRef =
        AccessTools.FieldRefAccess<NMerchantRoom, List<Player>>("_players");

    public static void Postfix(NMerchantRoom __instance)
    {
        List<Player> players = PlayersRef(__instance);
        for (int i = 0; i < __instance.PlayerVisuals.Count && i < players.Count; i++)
        {
            if (players[i]?.Character is PlumaCharacter)
            {
                PlumaWorldVisualsSkinPatch.ApplySkinToMerchant(__instance.PlayerVisuals[i], players[i]);
            }
        }
    }
}

/// <summary>
/// 篝火房间创建完所有角色后，按玩家皮肤替换骨架并播放 Sit。
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
internal static class PlumaRestSiteSkinPatch
{
    private static readonly AccessTools.FieldRef<NRestSiteCharacter, int> CharacterIndexRef =
        AccessTools.FieldRefAccess<NRestSiteCharacter, int>("_characterIndex");

    public static void Postfix(NRestSiteRoom __instance)
    {
        foreach (NRestSiteCharacter character in __instance.Characters)
        {
            if (character.Player?.Character is PlumaCharacter)
            {
                PlumaWorldVisualsSkinPatch.ApplySkinToRestSite(character, CharacterIndexRef(character));
            }
        }
    }
}

/// <summary>
/// 假商贩事件：RitsuLib 会复用商店角色场景（NMerchantCharacter）摆摊位，
/// 这里同样按玩家皮肤替换骨架。摊位节点通过 MoveChildSafely(..., 0) 插入，
/// 因此容器里的顺序与玩家列表相反。
/// </summary>
[HarmonyPatch(typeof(NFakeMerchant), "AfterRoomIsLoaded")]
internal static class PlumaFakeMerchantSkinPatch
{
    private static readonly AccessTools.FieldRef<NFakeMerchant, List<Player>> PlayersRef =
        AccessTools.FieldRefAccess<NFakeMerchant, List<Player>>("_players");

    private static readonly AccessTools.FieldRef<NFakeMerchant, Control> ContainerRef =
        AccessTools.FieldRefAccess<NFakeMerchant, Control>("_characterContainer");

    public static void Postfix(NFakeMerchant __instance)
    {
        List<Player> players = PlayersRef(__instance);
        Control container = ContainerRef(__instance);
        List<NMerchantCharacter> visuals = container.GetChildren().OfType<NMerchantCharacter>().ToList();
        for (int k = 0; k < visuals.Count && k < players.Count; k++)
        {
            int playerIndex = players.Count - 1 - k;
            if (players[playerIndex]?.Character is PlumaCharacter)
            {
                PlumaWorldVisualsSkinPatch.ApplySkinToMerchant(visuals[k], players[playerIndex]);
            }
        }
    }
}

/// <summary>
/// 屏蔽 NRestSiteCharacter 对 Spine 子节点的原版 act 动画遍历。
///
/// 原版会在 _Ready 里对所有 SpineSprite 播放 overgrowth_loop / hive_loop / glory_loop，
/// 我们的空手版骨架没有这些名字（只有 Sit），会打一条 "Can not find animation" 报错。
/// 返回空列表后原版不会碰我们的骨架；Sit 由 PlumaRestSiteSkinPatch 统一播放，
/// 翻转（FlipX）也由该补丁按玩家编号自己处理。
/// </summary>
[HarmonyPatch(typeof(NRestSiteCharacter), "GetChildSpineNodes")]
internal static class PlumaRestSiteSpineNodesPatch
{
    public static bool Prefix(NRestSiteCharacter __instance, ref IEnumerable<Node2D> __result)
    {
        if (__instance.Player?.Character is PlumaCharacter)
        {
            __result = Enumerable.Empty<Node2D>();
            return false;
        }
        return true;
    }
}
