using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 羽毛笔「渐入佳境」层数特效 + 收割者特性治疗的银白十字特效。
/// 参考明日方舟原版表现：每次叠层出现向上飘的小箭头，通过特性治疗时出现银白十字，
/// 叠满 12 层时额外爆出一圈光环与星芒。
/// 全部用 Sprite2D（叠加混合）+ Tween 在运行时搭建，不需要 .tscn，也不占用 pck 里的场景。
/// </summary>
public static class FlowVfx
{
    /// <summary>原版层数上限（满层）。</summary>
    public const int MaxStacks = 12;

    private const string TexDir = "res://pluma/images/effects/flow/";

    private static readonly Dictionary<string, Texture2D> TextureCache = new();
    private static CanvasItemMaterial? _additive;

    // 银白偏冷，和原版特效的高光一致
    private static readonly Color Silver = new(0.93f, 0.97f, 1.00f);
    private static readonly Color StarCyan = new(0.78f, 0.90f, 1.00f);

    /// <summary>
    /// 获得渐入佳境层数时调用。<paramref name="gained"/> 为本次增加层数，<paramref name="total"/> 为当前总层数。
    /// 只在「跨过 12 层」那一次播放满层爆光。
    /// </summary>
    public static void PlayStackGain(Creature owner, int gained, int total)
    {
        if (gained <= 0) return;

        Node2D? root = Prepare(owner);
        if (root == null) return;

        // 兜底：钩子里拿到的总层数若还没刷新，至少按本次增加量算
        if (total < gained) total = gained;
        int before = total - gained;
        bool reachedMax = total >= MaxStacks && before < MaxStacks;

        // 一次最多 3 支箭，避免连锁叠层时糊成一片
        int count = Mathf.Clamp(gained, 1, 3);
        for (int i = 0; i < count; i++)
        {
            // 用黄金比例错开横向位置：确定性、不用随机数，多支箭不会重叠
            float t = ((before + i) * 0.6180339887f) % 1f;
            float x = (t - 0.5f) * 34f;
            Sprite2D arrow = MakeSprite(root, "arrow.png", new Vector2(x, 26f + i * 5f), 0.5f, Silver);
            Rising(arrow, 0.5f, 0.06f + i * 0.07f, 58f);
        }

        if (reachedMax)
        {
            MaxBurst(root);
            AutoFree(root, 2.0f);
        }
        else if (total >= MaxStacks)
        {
            // 已满层还继续叠：脚下补一圈轻脉冲
            Pulse(root);
            AutoFree(root, 1.2f);
        }
        else
        {
            AutoFree(root, 1.1f);
        }
    }

    /// <summary>收割者特性回复生命时调用：银白十字 + 柔光。</summary>
    public static void PlayHeal(Creature owner)
    {
        Node2D? root = Prepare(owner);
        if (root == null) return;

        // 十字本体：放大-回缩-淡出，同时略微上浮
        Sprite2D cross = MakeSprite(root, "cross.png", new Vector2(0f, 6f), 0.35f, Silver);
        cross.Scale = Vector2.One * 0.25f;
        Tween tw = cross.CreateTween();
        tw.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(cross, "scale", Vector2.One * 0.85f, 0.20f);
        tw.TweenProperty(cross, "scale", Vector2.One * 0.72f, 0.28f);
        tw.Parallel().TweenProperty(cross, "modulate:a", 0f, 0.34f);
        tw.Parallel().TweenProperty(cross, "position", new Vector2(0f, -14f), 0.34f);

        // 背后的柔光
        Sprite2D glow = MakeSprite(root, "glow_soft.png", new Vector2(0f, 6f), 0.5f, StarCyan);
        glow.Modulate = new Color(glow.Modulate, 0.75f);
        Tween g = glow.CreateTween();
        g.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        g.TweenProperty(glow, "scale", Vector2.One * 1.5f, 0.5f);
        g.Parallel().TweenProperty(glow, "modulate:a", 0f, 0.5f);

        // 两颗小星点缀
        for (int i = 0; i < 2; i++)
        {
            float dir = i == 0 ? -1f : 1f;
            Sprite2D star = MakeSprite(root, "star.png", new Vector2(dir * 26f, 4f), 0.22f, Silver);
            Rising(star, 0.35f, 0.05f + i * 0.06f, 22f, drift: dir * 12f);
        }

        AutoFree(root, 0.75f);
    }

    // ---------------------------------------------------------------- 内部实现

    private static void MaxBurst(Node2D root)
    {
        // 大光环
        Sprite2D ring = MakeSprite(root, "ring_soft.png", new Vector2(0f, 8f), 0.35f, StarCyan);
        Tween r = ring.CreateTween();
        r.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        r.TweenProperty(ring, "scale", Vector2.One * 1.7f, 0.7f);
        r.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.7f);

        // 中心爆光
        Sprite2D flash = MakeSprite(root, "glow_soft.png", new Vector2(0f, 4f), 0.6f, Silver);
        flash.Modulate = new Color(flash.Modulate, 0.9f);
        Tween f = flash.CreateTween();
        f.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        f.TweenProperty(flash, "scale", Vector2.One * 2.0f, 0.45f);
        f.Parallel().TweenProperty(flash, "modulate:a", 0f, 0.45f);

        // 六个星芒向外飞出
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.Tau * i / 6f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.7f);
            Sprite2D star = MakeSprite(root, "star.png", dir * 10f, 0.30f, Silver);
            Vector2 target = dir * 58f + new Vector2(0f, -18f);
            Tween s = star.CreateTween();
            s.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            s.TweenProperty(star, "position", target, 0.55f);
            s.Parallel().TweenProperty(star, "scale", Vector2.One * 0.10f, 0.55f);
            s.Parallel().TweenProperty(star, "modulate:a", 0f, 0.55f).SetDelay(0.15f);
            s.Parallel().TweenProperty(star, "rotation", ang * 2f, 0.55f);
        }

        // 三支加长箭矢冲天
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 18f;
            Sprite2D big = MakeSprite(root, "arrow.png", new Vector2(x, 20f), 0.72f, Silver);
            Rising(big, 1.1f, 0.10f + i * 0.05f, 96f);
        }
    }

    private static void Pulse(Node2D root)
    {
        Sprite2D ring = MakeSprite(root, "ring_soft.png", new Vector2(0f, 10f), 0.25f, StarCyan);
        ring.Modulate = new Color(ring.Modulate, 0.65f);
        Tween t = ring.CreateTween();
        t.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        t.TweenProperty(ring, "scale", Vector2.One * 1.05f, 0.5f);
        t.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.5f);
    }

    /// <summary>小箭头/星芒的上浮动画：淡入 → 上移 → 淡出。</summary>
    private static void Rising(Sprite2D node, float duration, float delay, float rise, float drift = 0f)
    {
        Vector2 start = node.Position;
        var target = new Vector2(start.X + drift, start.Y - rise);
        float baseScale = node.Scale.X;

        node.Modulate = new Color(node.Modulate, 0f);
        Tween tw = node.CreateTween();
        tw.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(node, "position", target, duration).SetDelay(delay);
        tw.Parallel().TweenProperty(node, "scale", Vector2.One * (baseScale * 1.35f), duration * 0.6f).SetDelay(delay);
        tw.Parallel().TweenProperty(node, "modulate:a", 1f, 0.10f).SetDelay(delay);
        tw.TweenProperty(node, "modulate:a", 0f, duration * 0.45f);
    }

    /// <summary>取特效容器与出生点，建一个临时根节点；不满足条件时返回 null。</summary>
    private static Node2D? Prepare(Creature? owner)
    {
        if (owner == null || TestMode.IsOn) return null;

        Control? container = owner.GetVfxContainer();
        if (container == null) return null;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (creatureNode == null) return null;

        // 战斗特效容器是 identity 变换，加入树之前设好位置即可
        var root = new Node2D { Position = creatureNode.VfxSpawnPosition };
        container.AddChildSafely(root);
        return root;
    }

    private static Sprite2D MakeSprite(Node2D parent, string texture, Vector2 position, float scale, Color color)
    {
        var sprite = new Sprite2D
        {
            Texture = Load(texture),
            Centered = true,
            Position = position,
            Scale = Vector2.One * scale,
            Modulate = color,
            Material = Additive()
        };
        parent.AddChild(sprite);
        return sprite;
    }

    private static void AutoFree(Node2D root, float seconds)
    {
        Tween t = root.CreateTween();
        t.TweenInterval(seconds);
        t.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(root)) root.QueueFree();
        }));
    }

    private static CanvasItemMaterial Additive()
    {
        return _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    }

    private static Texture2D? Load(string name)
    {
        string path = TexDir + name;
        if (TextureCache.TryGetValue(path, out Texture2D? cached)) return cached;

        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[pluma] 缺少渐入佳境特效贴图：{path}");
            return null;
        }

        var tex = GD.Load<Texture2D>(path);
        TextureCache[path] = tex;
        return tex;
    }
}
