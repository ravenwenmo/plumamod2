using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 明日方舟「官方同款」特效播放器（素材 = 官方特效 prefab 的离线烘焙结果）。
///
/// 官方绑定关系（来自 efx 数据表，不是猜的）：
///   · 天赋 1 攻速叠层 <c>crow_t_1[atk_speed]</c> → <c>crow_attack_buff_02</c>（星点 + 箭羽拖尾的循环光环）
///   · 特性回复 <c>crow_trait[heal][effect]</c> → <c>crow_attack_buff_01</c>（爆闪 + 白十字）
///   · 天赋 1 满层 <c>crow_t_1[atk_speed][max]</c> → <c>crow_attack_buff_03</c>（扩散环是 Mesh 动画，粒子烘焙拿不到）
///
/// 烘焙：<c>.tmp_rt/fx_bake.py</c> 逐粒子复刻官方 ParticleSystem（含 Dissolve 遮罩），
/// 按 30fps 渲成 256×256 帧，打成 8 列精灵表，运行时用 AnimatedSprite2D + 叠加混合播放。
/// </summary>
public static class OfficialFx
{
    public enum Kind
    {
        /// <summary>渐入佳境持有期间持续播放的循环光环。</summary>
        FlowAura,

        /// <summary>特性治疗自身时的一次性闪光。</summary>
        Heal,
    }

    private const string Dir = "res://pluma/images/effects/official/";

    // 银白偏冷，和原版高光一致
    private static readonly Color Silver = new(0.95f, 0.97f, 1.00f);
    private static readonly Color White = new(1f, 1f, 1f);

    private static readonly Dictionary<string, SpriteFrames> FrameCache = new();
    private static CanvasItemMaterial? _additive;

    /// <summary>精灵表规格：帧数 / 列数 / 单帧边长 / 帧率 / 是否循环。</summary>
    private readonly record struct SheetSpec(string File, int Frames, int Cols, int Cell, float Fps, bool Loop);

    private static SheetSpec Spec(Kind kind) => kind switch
    {
        Kind.FlowAura => new SheetSpec("flow_aura", 90, 8, 256, 30f, true),
        _ => new SheetSpec("heal_cross", 36, 8, 256, 30f, false),
    };

    /// <summary>
    /// 创建挂在 <paramref name="owner"/> 身上的循环光环（渐入佳境持有期间一直存在）。
    /// 返回 null 表示当前环境不能播（无战斗 / 找不到小人节点）。
    /// </summary>
    public static Node2D? CreateAura(Creature? owner, float scale = 2.5f)
    {
        return Build(owner, Kind.FlowAura, scale, Silver, anchorBottom: false, loop: true);
    }

    /// <summary>特性治疗自身时的一次性特效（白十字爆闪，1.5 倍、底边不动）。</summary>
    public static void PlayHeal(Creature? owner)
    {
        Build(owner, Kind.Heal, 1.5f, Silver, anchorBottom: true, loop: false);
    }

    private static Node2D? Build(Creature? owner, Kind kind, float scale, Color tint, bool anchorBottom, bool loop)
    {
        if (TestMode.IsOn) return null;

        NCreature? node = owner?.GetCreatureNode();
        if (node == null)
        {
            GD.PushWarning($"[pluma][OfficialFx] 找不到小人节点，跳过 {kind} 特效");
            return null;
        }

        SheetSpec spec = Spec(kind);
        SpriteFrames? frames = LoadFrames(spec);
        if (frames == null) return null;

        float globalScale = Mathf.Abs(node.GetGlobalTransform().Scale.X);
        if (globalScale > 0.01f && Mathf.Abs(globalScale - 1f) > 0.02f) scale /= globalScale;

        var root = new Node2D
        {
            Name = $"PlumaFx_{kind}",
            ZIndex = loop ? 35 : 40,
            ZAsRelative = true,
            Position = node.MakeCanvasPositionLocal(node.VfxSpawnPosition),
        };

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "default",
            Centered = true,
            Scale = new Vector2(scale, scale),
            Modulate = tint,
            Material = AdditiveMaterial(),
            ShowBehindParent = false,
        };
        if (anchorBottom)
        {
            // 以帧中心为原点放大，会同时抬高底边；这里把中心整体上移补偿，让底边停在原处
            sprite.Position = new Vector2(0f, -spec.Cell * (scale - 1f) * 0.5f);
        }

        root.AddChildSafely(sprite);
        node.AddChildSafely(root);
        sprite.Play();

        if (!loop)
        {
            float duration = spec.Frames / spec.Fps;
            Tween tween = root.CreateTween();
            tween.TweenInterval(duration);
            tween.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(root)) root.QueueFree();
            }));
        }

        return root;
    }

    private static SpriteFrames? LoadFrames(SheetSpec spec)
    {
        string key = spec.File;
        if (FrameCache.TryGetValue(key, out SpriteFrames? cached) && GodotObject.IsInstanceValid(cached)) return cached;

        string path = Dir + spec.File + ".png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[pluma][OfficialFx] 缺少特效精灵表：{path}");
            return null;
        }

        var atlas = GD.Load<Texture2D>(path);
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation("default");
        frames.SetAnimationSpeed("default", spec.Fps);
        frames.SetAnimationLoop("default", spec.Loop);

        for (int i = 0; i < spec.Frames; i++)
        {
            var tex = new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(i % spec.Cols * spec.Cell, i / spec.Cols * spec.Cell, spec.Cell, spec.Cell),
            };
            frames.AddFrame("default", tex);
        }

        FrameCache[key] = frames;
        return frames;
    }

    private static CanvasItemMaterial AdditiveMaterial()
    {
        _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        return _additive;
    }
}
