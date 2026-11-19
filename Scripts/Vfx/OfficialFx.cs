using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 明日方舟「官方同款」特效播放器。
///
/// 素材来源：官方特效 prefab（卫戍协议项目已把 Unity prefab 逐字段导出为 JSON）
///   · 渐入佳境叠层 → crow_attack_buff_01（shizi_03 十字 + mask_30 柔光，白十字向上扩散）
///   · 满 12 层     → 同一套 prefab 的完整 2 秒版本，放大 1.5 倍、叠一层 1.35 倍
///   · 特性治疗     → common_heal_hit_01（shizi_02 十字 + star_12 星点 + star_02 柔光），
///                    原版是青绿色，这里按需求染成银白色
///
/// 烘焙方式：.tmp_rt/fx_bake.py 逐粒子复刻官方 ParticleSystem 的发射/生命周期/尺寸/颜色曲线，
/// 按 30fps 渲染成 256×256 的帧，再打成 8 列的精灵表（预乘色 + alpha 二值化，配合叠加混合）。
/// 所以这里的素材是"官方 prefab 渲染结果"，不是手绘贴图。
///
/// 播放方式：直接挂在 NCreature 上（跟着小人走），ZIndex 40，叠加混合，播完自毁。
/// </summary>
public static class OfficialFx
{
    public enum Kind
    {
        StackGain,
        MaxStack,
        Heal,
    }

    private const string Dir = "res://pluma/images/effects/official/";

    // 银白偏冷，和原版高光一致
    private static readonly Color Silver = new(0.95f, 0.97f, 1.00f);
    private static readonly Color White = new(1f, 1f, 1f);

    private static readonly Dictionary<string, SpriteFrames> FrameCache = new();
    private static CanvasItemMaterial? _additive;

    /// <summary>精灵表规格：帧数 / 列数 / 单帧边长 / 帧率。</summary>
    private readonly record struct SheetSpec(string File, int Frames, int Cols, int Cell, float Fps);

    private static SheetSpec Spec(Kind kind) => kind switch
    {
        Kind.StackGain => new SheetSpec("crow_buff_stack", 36, 8, 256, 30f),
        Kind.MaxStack => new SheetSpec("crow_buff_max", 60, 8, 256, 30f),
        _ => new SheetSpec("heal_cross", 38, 8, 256, 30f),
    };

    /// <summary>
    /// 在<paramref name="owner"/>身上播一段官方特效。
    /// </summary>
    /// <param name="scale">整体缩放（相对小人身高；1.0 约等于半个小人高）。</param>
    /// <param name="tint">染色（叠加混合下等价于提亮/偏色）。</param>
    /// <param name="anchorBottom">true 时缩放后保持底边位置不变。</param>
    public static void Play(Creature? owner, Kind kind, float scale = 1f, Color? tint = null, bool anchorBottom = false)
    {
        if (TestMode.IsOn) return;

        NCreature? node = owner?.GetCreatureNode();
        if (node == null)
        {
            GD.PushWarning($"[pluma][OfficialFx] 找不到小人节点，跳过 {kind} 特效");
            return;
        }

        SheetSpec spec = Spec(kind);
        SpriteFrames? frames = LoadFrames(spec);
        if (frames == null) return;

        float globalScale = Mathf.Abs(node.GetGlobalTransform().Scale.X);
        if (globalScale > 0.01f && Mathf.Abs(globalScale - 1f) > 0.02f) scale /= globalScale;

        var root = new Node2D
        {
            Name = $"PlumaFx_{kind}",
            ZIndex = 40,
            ZAsRelative = true,
            Position = node.MakeCanvasPositionLocal(node.VfxSpawnPosition),
        };

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "default",
            Centered = true,
            Scale = new Vector2(scale, scale),
            Modulate = tint ?? White,
            Material = AdditiveMaterial(),
            // 叠加混合下关闭按像素排序，避免和裁剪/半透明排序打架
            ShowBehindParent = false,
        };
        if (anchorBottom)
        {
            // 以帧中心为原点放大，会同时抬高底边；这里把中心整体下移补偿，让底边停在原处
            sprite.Position = new Vector2(0f, -spec.Cell * (scale - 1f) * 0.5f);
        }

        root.AddChildSafely(sprite);
        node.AddChildSafely(root);
        sprite.Play();

        float duration = spec.Frames / spec.Fps;
        Tween tween = root.CreateTween();
        tween.TweenInterval(duration);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(root)) root.QueueFree();
        }));
    }

    /// <summary>渐入佳境增层：每层播一次；跨过 12 层额外再叠一层大特效。</summary>
    public static void PlayStackGain(Creature? owner, int gained, int total)
    {
        if (gained <= 0) return;

        int count = Mathf.Clamp(gained, 1, 3);
        int before = Mathf.Max(0, total - gained);
        bool reachedMax = total >= FlowVfx.MaxStacks && before < FlowVfx.MaxStacks;

        GD.Print($"[pluma][OfficialFx] 渐入佳境 +{gained}（共 {total} 层，满层={reachedMax}）");

        for (int i = 0; i < count; i++)
        {
            // 多出来的层数用不同缩放错开一点，避免完全重叠
            float s = 1.60f + 0.18f * Mathf.Min(i, 2);
            Play(owner, Kind.StackGain, s, Silver);
        }

        if (reachedMax)
        {
            Play(owner, Kind.MaxStack, 2.20f, White);
            Play(owner, Kind.MaxStack, 1.70f, Silver);
        }
    }

    /// <summary>治疗特效：原版 common_heal_hit_01 染成银白，放大 1.5 倍、底边不动。</summary>
    public static void PlayHeal(Creature? owner)
    {
        Play(owner, Kind.Heal, 1.5f, Silver, anchorBottom: true);
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
        frames.SetAnimationLoop("default", false);

        int rows = Mathf.CeilToInt(spec.Frames / (float)spec.Cols);
        _ = rows;
        for (int i = 0; i < spec.Frames; i++)
        {
            int col = i % spec.Cols;
            int row = i / spec.Cols;
            var tex = new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(col * spec.Cell, row * spec.Cell, spec.Cell, spec.Cell),
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
