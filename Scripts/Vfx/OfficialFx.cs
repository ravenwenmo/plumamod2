using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using FxEmitter = Pluma.Scripts.Vfx.OfficialFxInstance.FxEmitter;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 明日方舟「官方同款」特效播放器（按官方 prefab 的参数逐粒子复刻，不再烘焙成精灵表）。
///
/// 官方绑定关系（来自 buff_template_data / charpack，不是猜的）：
///   · 天赋 1 攻速叠层 <c>crow_t_1[atk_speed]</c>（lifeTime INFINITY）→ <c>crow_attack_buff_02</c>
///     = star（黑羽流光，10/s）+ trail（白色箭羽，7/s）两套循环粒子
///   · 特性回复 <c>crow_trait[heal]</c> 的治疗 buff（FixedValueHeal BUFF_OWNER，LIMITED）→ <c>crow_attack_buff_01</c>
///     = baoshan1（flow_75 被 mask_30 溶解遮罩切开的一次爆闪）+ shizi_1（7 枚 shizi_03 十字爆散）
///   · 天赋 1 满层 <c>crow_t_1[atk_speed][max]</c> → <c>crow_attack_buff_03</c>（kuohuan 网格环，仍是旧烘焙）
///
/// 坐标系：官方 prefab 以「米」为单位（干员身高约 1.6m），这里按 <see cref="PxPerUnit"/> 换算成像素；
/// 官方战场 Z 轴向上、镜头俯视，本 mod 是纯 2D 侧视，所以把「向场上纵深」近似映射为「屏幕向上」。
/// </summary>
public static class OfficialFx
{
    /// <summary>测不到小人包围盒时的兜底：1 个 Unity 单位（米）≈ 多少画布像素。</summary>
    private const float FallbackPxPerUnit = 118f;

    /// <summary>
    /// 方舟里干员小人（含立绘整体）大约 1.8 个 Unity 单位高 —— 特效 prefab 的米制数值就是按这个比例
    /// 与小人同屏的。用它把「小人实际像素高度」换算成「像素/单位」，特效大小才和原版同比例。
    /// </summary>
    private const float ChibiHeightUnits = 1.8f;

    /// <summary>测不到包围盒时的兜底：小人可见高度的一半（像素）。</summary>
    private const float FallbackHalfHeight = 140f;

    /// <summary>
    /// 按小人当前的实际绘制尺寸换算「像素/单位」，并给出「脚底到包围盒中心」的像素距离。
    /// Bounds 由 spine 的包围盒驱动（`NCreature.UpdateBounds`），已含 Visuals 的 0.7 缩放，
    /// 所以这里拿到的就是屏幕上真正看到的高度。
    /// </summary>
    private static float MeasurePxPerUnit(NCreature node, out float halfHeightPx)
    {
        halfHeightPx = FallbackHalfHeight;
        try
        {
            Rect2 bounds = node.Visuals.Bounds.GetGlobalRect();
            if (bounds.Size.Y > 1f)
            {
                halfHeightPx = bounds.Size.Y * 0.5f;
                return Mathf.Max(40f, bounds.Size.Y / ChibiHeightUnits);
            }
        }
        catch (System.Exception e)
        {
            GD.PushWarning($"[pluma][OfficialFx] 读取小人包围盒失败，用兜底尺寸：{e.Message}");
        }

        return FallbackPxPerUnit;
    }

    private const string FxDir = "res://pluma/images/effects/fx/";
    private const string SheetDir = "res://pluma/images/effects/official/";

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static readonly Dictionary<string, Shader> Shaders = new();
    private static SpriteFrames? _maxStackFrames;

    // ---- 对外接口 -------------------------------------------------------------------------------

    /// <summary>渐入佳境持有期间挂在身上的循环光环（官方 crow_attack_buff_02）。</summary>
    public static Node2D? CreateAura(Creature? owner)
    {
        NCreature? node = Resolve(owner, "渐入佳境光环");
        if (node == null) return null;

        float pxPerUnit = MeasurePxPerUnit(node, out _);
        var fx = new OfficialFxInstance
        {
            Name = "PlumaFlowAura",
            ZIndex = 35,
            PxPerUnit = pxPerUnit,
        };
        float unit = UnitScale(node);

        // star：12 层攻速天赋的黑羽流光。官方 _TintColor = (0,0,0,0.392)，AlphaBlend（普通混合）——
        // 也就是「深色羽影」，不是白色星点；拉长 2 倍（renderMode=stretch + lengthScale=2）。
        fx.Add(new FxEmitter
        {
            Name = "star",
            Main = "star_02",
            Additive = false,
            Tint = new Color(0f, 0f, 0f, 0.392157f),
            Loop = true,
            Rate = 10f,
            LifeMin = 0.2f,
            LifeMax = 0.4f,
            SpeedMin = 3.5f,
            SpeedMax = 3.5f,
            SizeXMin = 0.02f,
            SizeXMax = 0.04f,
            SizeYMin = 0.2f,
            SizeYMax = 0.4f,
            SizeXKeys = new[] { new Vector2(0f, 1f), new Vector2(1f, 0f) },
            SizeYKeys = new[] { new Vector2(0f, 0.0925903f), new Vector2(0.171866f, 0.839315f), new Vector2(1f, 1f) },
            AlphaKeys = new[] { new Vector2(0f, 0f), new Vector2(0.161761f, 1f), new Vector2(1f, 0f) },
            Stretch = true,
            LengthScale = 2f,
            SpawnHalfWidth = 0.35f,
            NodeYUp = 0.387f,
            Order = 0,
            Prewarm = 1.5f,
        });

        // trail：同一份层叠 buff 的第二层，白色箭羽（trail_116），排序在 star 之上。
        fx.Add(new FxEmitter
        {
            Name = "trail",
            Main = "trail_116",
            Additive = false,
            Tint = Colors.White,
            Loop = true,
            Rate = 7f,
            LifeMin = 0.35f,
            LifeMax = 0.5f,
            SpeedMin = 1f,
            SpeedMax = 3f,
            SizeXMin = 0.06f,
            SizeXMax = 0.11f,
            SizeYMin = 0.08f,
            SizeYMax = 0.1f,
            SizeXKeys = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) },
            SizeYKeys = new[] { new Vector2(0f, 1f), new Vector2(1f, 1f) },
            AlphaKeys = new[] { new Vector2(0f, 0f), new Vector2(0.308827f, 0.509804f), new Vector2(1f, 0f) },
            Stretch = true,
            LengthScale = 2f,
            SpawnHalfWidth = 0.3f,
            NodeYUp = 0.161f,
            FlipX = true,
            Order = 1,
            Prewarm = 1.5f,
        });

        Attach(fx, node, unit);
        return fx;
    }

    /// <summary>收割者 / 遗物治疗自身时的一次性特效（官方 crow_attack_buff_01）。</summary>
    public static void PlayHeal(Creature? owner)
    {
        NCreature? node = Resolve(owner, "治疗特效");
        if (node == null) return;

        float pxPerUnit = MeasurePxPerUnit(node, out _);
        var fx = new OfficialFxInstance
        {
            Name = "PlumaHealFx",
            ZIndex = 40,
            PxPerUnit = pxPerUnit,
        };
        float unit = UnitScale(node);

        // baoshan1：flow_75 主体 + mask_30 溶解遮罩，Additive；单颗、原地放大再收缩，Roll 30°。
        // 【临时】整层注释掉，只保留 shizi_1 十字，用来单独看十字的效果。
#if false
        fx.Add(new FxEmitter
        {
            Name = "baoshan1",
            Main = "flow_75",
            Dissolve = "mask_30",
            Additive = true,
            Tint = Colors.White,
            UvMain = new Vector4(0.5f, 0.5f, 0f, 0f),
            Amount = 0.25f,
            Border = 0.5f,
            Burst = 1,
            LifeMin = 0.45f,
            LifeMax = 0.45f,
            SpeedMin = 0f,
            SpeedMax = 0f,
            SizeXMin = 0.8f,
            SizeXMax = 1f,
            SizeYMin = 1f,
            SizeYMax = 1f,
            MaxSize = 0.75f,
            // SizeModule：scalar 1.5 × 曲线（separateAxes=false，两轴共用同一条）
            SizeXKeys = new[] { new Vector2(0f, 0.509148f), new Vector2(0.276305f, 1.5f), new Vector2(1f, 0.835327f) },
            SizeYKeys = new[] { new Vector2(0f, 0.509148f), new Vector2(0.276305f, 1.5f), new Vector2(1f, 0.835327f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.179416f, 1f),
                new Vector2(0.441184f, 0.627451f), new Vector2(1f, 0f),
            },
            // 官方 startColor 是 TwoColors：白 → 灰（alpha 0.275），逐粒子随机
            StartColor = Colors.White,
            StartColorB = new Color(0.352941f, 0.352941f, 0.352941f, 0.27451f),
            // 官方这颗雾团同样用 flow_75，但被 bloom 洗成灰白；这里直接把颜色拉白，
            // 否则会被看成「flow_75 溢出到十字外面」
            Opacity = 0.55f,
            RgbBoost = 1.0f,
            Whitening = 0.8f,
            Glow = 0.22f,
            GlowScale = 1.7f,
            Stretch = false,
            RotationDeg = 30f,
            NodeYUp = 0.9f,
            Order = 0,
        });
#endif

        // shizi_1：7 枚十字爆散，主 UV 按 _UVTween 持续横向滚动（flow_75 的噪声在里面流）。
        fx.Add(new FxEmitter
        {
            Name = "shizi_1",
            Main = "flow_75",
            Dissolve = "shizi_03",
            Additive = true,
            Tint = Colors.White,
            UvMain = new Vector4(0.3f, 0.3f, 0f, 0f),
            Amount = 0.35f,
            Border = 0.35f,
            UvScroll = 0.5f,
            Burst = 7,
            LifeMin = 0.3f,
            LifeMax = 1f,
            SpeedMin = 1f,
            SpeedMax = 3.5f,
            LimitSpeed = 0f,
            Dampen = 0.1f,
            SizeXMin = 0.1f,
            SizeXMax = 0.25f,
            SizeYMin = 0.1f,
            SizeYMax = 0.25f,
            SharedSize = true,
            MaxSize = 0.5f,
            // SizeModule：x 缩放曲线；y 是 scalar 10 × 曲线（十字被纵向拉长）
            SizeXKeys = new[] { new Vector2(0f, 0f), new Vector2(0.168952f, 0.86909f), new Vector2(1f, 1f) },
            SizeYKeys = new[] { new Vector2(0f, 2.15385f), new Vector2(0.150405f, 1.04689f), new Vector2(1f, 0.745525f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.152941f, 1f), new Vector2(0.461769f, 1f),
                new Vector2(0.788235f, 0.772549f), new Vector2(1f, 0f),
            },
            // 官方 startColor：alpha 在 0.309 与 1.0 之间随机，十字有浓有淡
            StartColor = new Color(1f, 1f, 1f, 0.309f),
            StartColorB = Colors.White,
            // 官方靠 bloom 把细十字顶亮，StS2 没有 bloom，只能加大最终 alpha
            // 白核 + 保留 flow_75 的噪点纹理（官方是 flow_75 × shizi_03 遮罩、发光混合）
            Opacity = 2.4f,
            RgbBoost = 1.8f,
            Whitening = 0.35f,
            Glow = 0.45f,
            GlowScale = 2.0f,
            Stretch = false,
            RotationDeg = 90f,
            // 官方 ShapeModule：type 5 = box，`radius 0.5` 是盒子半边长，`m_Scale [0, 0.8, 0]`
            // 把盒子在 y 方向压到 0.8 倍 —— 也就是在胯部附近撒出一块 ~1.0 × 0.9 米的方形区域，
            // 再叠上 25° 的向上锥。之前按「0.8 米的一条线」处理，纵向只剩一条，所以看起来被压扁。
            SpawnHalfWidth = 0.5f,
            SpawnHalfHeight = 0.45f,
            NodeYUp = 0.52f,
            // 官方 angle 25：绕 +Z（世界 Y）的 25° 锥，换算成单侧半角是 12.5°
            SpreadDeg = 12.5f,
            Order = 1,
        });

        Attach(fx, node, unit);
        fx.PlayOnce(1.6f);
    }

    /// <summary>进入战斗、部署动画时的干员入场特效（官方 crow_start_01）。</summary>
    public static void PlayDeploy(Creature? owner)
    {
        NCreature? node = Resolve(owner, "入场特效");
        if (node == null) return;

        float pxPerUnit = MeasurePxPerUnit(node, out _);
        var fx = new OfficialFxInstance
        {
            Name = "PlumaDeployFx",
            ZIndex = 45,
            PxPerUnit = pxPerUnit,
        };
        float unit = UnitScale(node);

        // smoke：10 团烟，6×6 序列帧，向上/四周散开后立刻刹住
        fx.Add(new FxEmitter
        {
            Name = "smoke",
            Main = "smoke_17",
            // 加色混合：普通混合会把烟雾背后的场景压暗，加色只提亮、不会加深背景
            Additive = true,
            Tint = Colors.White,
            Burst = 10,
            LifeMin = 0.5f,
            LifeMax = 1.5f,
            SpeedMin = 1f,
            SpeedMax = 2f,
            SizeXMin = 0.4f,
            SizeXMax = 0.8f,
            SizeYMin = 0.4f,
            SizeYMax = 0.8f,
            SizeXKeys = new[] { new Vector2(0f, 0.0869446f), new Vector2(0.228922f, 0.819419f), new Vector2(1f, 1f) },
            // SizeModule 的 y 是 scalar 1.5 × 曲线
            SizeYKeys = new[] { new Vector2(0f, 0.681003f), new Vector2(1f, 0.874008f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.164706f, 1f),
                new Vector2(0.500008f, 0.356863f), new Vector2(1f, 0f),
            },
            LimitSpeed = 0.03f,
            Dampen = 0.2f,
            FlipCols = 6,
            FlipRows = 6,
            RandomRotation = true,
            // 官方 smoke 是 billboard 团烟；用 stretch 会把每颗拉成又高又窄的竖条
            Stretch = false,
            SharedSize = true,
            MaxSize = 0.7f,
            Opacity = 1.2f,
            Glow = 0.3f,
            GlowScale = 1.6f,
            // 官方 startColor 是深灰两色渐变（0.441 → 0.066），不是白烟
            StartColor = new Color(0.441176f, 0.441176f, 0.441176f, 0.503f),
            StartColorB = new Color(0.066176f, 0.066176f, 0.066176f, 0.284f),
            SpawnHalfWidth = 0.22f,
            SpawnHalfHeight = 0.14f,
            NodeYUp = 0.55f,
            Order = 1,
        });

        // crow2：10 根羽毛，向外炸开后减速、边飞边转
        fx.Add(new FxEmitter
        {
            Name = "crow2",
            Main = "crow_02",
            Additive = false,
            Tint = Colors.White,
            Burst = 10,
            LifeMin = 0.7f,
            LifeMax = 1.5f,
            SpeedMin = 2f,
            SpeedMax = 5f,
            SizeXMin = 0.05f,
            SizeXMax = 0.15f,
            SizeYMin = 0.05f,
            SizeYMax = 0.15f,
            SharedSize = true,
            SizeXKeys = new[] { new Vector2(0f, 0.0909119f), new Vector2(0.198833f, 1f), new Vector2(1f, 0.405109f) },
            SizeYKeys = new[] { new Vector2(0f, 0.0909119f), new Vector2(0.198833f, 1f), new Vector2(1f, 0.405109f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.173526f, 1f),
                new Vector2(0.679408f, 0.905882f), new Vector2(1f, 0f),
            },
            LimitSpeed = 0f,
            Dampen = 0.18f,
            RandomRotation = true,
            SpinMin = -5.23599f,
            SpinMax = 6.98132f,
            SpreadDeg = 360f,
            MaxSize = 0.5f,
            // 官方羽毛是纯黑（alpha 1 → 0.47），不是白色
            Stretch = false,
            StartColor = new Color(0f, 0f, 0f, 1f),
            StartColorB = new Color(0f, 0f, 0f, 0.470588f),
            SpawnHalfWidth = 0.12f,
            SpawnHalfHeight = 0.25f,
            NodeYUp = 0.75f,
            Order = 5,
        });

        // crow：火星/碎羽，4×4 序列帧，短促
        fx.Add(new FxEmitter
        {
            Name = "crow",
            Main = "flipbook_161",
            Additive = false,
            Tint = new Color(1f, 1f, 1f, 0.431373f),
            Burst = 12,
            LifeMin = 0.25f,
            LifeMax = 0.4f,
            SpeedMin = 1.5f,
            SpeedMax = 2.5f,
            SizeXMin = 0.25f,
            SizeXMax = 0.5f,
            SizeYMin = 0.25f,
            SizeYMax = 0.5f,
            SharedSize = true,
            SizeXKeys = new[] { new Vector2(0f, 0.0909119f), new Vector2(0.15121f, 0.692576f), new Vector2(1f, 1f) },
            SizeYKeys = new[] { new Vector2(0f, 0.0909119f), new Vector2(0.15121f, 0.692576f), new Vector2(1f, 1f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.173526f, 1f),
                new Vector2(0.679408f, 0.905882f), new Vector2(1f, 0f),
            },
            FlipCols = 4,
            FlipRows = 4,
            RandomRotation = true,
            SpreadDeg = 360f,
            Stretch = false,
            MaxSize = 0.5f,
            SpawnHalfWidth = 0.12f,
            SpawnHalfHeight = 0.2f,
            NodeYUp = 0.75f,
            Order = 5,
        });

        Attach(fx, node, unit);
        fx.PlayOnce(1.8f);
    }

    /// <summary>渐入佳境首次叠满 12 层：kuohuan 网格扩散环（buff_03，仍是旧烘焙）。</summary>
    public static void PlayMaxStack(Creature? owner)
    {
        if (TestMode.IsOn) return;

        NCreature? node = owner?.GetCreatureNode();
        if (node == null) return;

        SpriteFrames? frames = MaxStackFrames();
        if (frames == null) return;

        float pxPerUnit = MeasurePxPerUnit(node, out _);
        // 精灵表单帧 256px；官方 kuohuan 环直径约 1.3 个单位，按小人尺寸反推缩放
        float k = pxPerUnit * 1.3f / 256f;

        var root = new Node2D
        {
            Name = "PlumaMaxStackFx",
            ZIndex = 40,
            Position = node.MakeCanvasPositionLocal(node.VfxSpawnPosition),
            Scale = new Vector2(UnitScale(node), UnitScale(node)),
        };

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "default",
            Centered = true,
            Scale = new Vector2(k, k),
            // 环挂在脚底上方约 0.5 个单位处（官方 kuohuan 在 0.343~0.617）
            Position = new Vector2(0f, -0.5f * pxPerUnit),
        };
        root.AddChildSafely(sprite);
        node.AddChildSafely(root);
        sprite.Play();

        Tween tween = root.CreateTween();
        tween.TweenInterval(30f / 30f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(root)) root.QueueFree();
        }));
    }

    // ---- 内部工具 -------------------------------------------------------------------------------

    private static NCreature? Resolve(Creature? owner, string what)
    {
        if (TestMode.IsOn) return null;
        NCreature? node = owner?.GetCreatureNode();
        if (node == null) GD.PushWarning($"[pluma][OfficialFx] 找不到小人节点，跳过{what}");
        return node;
    }

    /// <summary>抵消小人节点自身的缩放（含左右翻转），让特效尺寸只由 prefab 的米制决定。</summary>
    private static float UnitScale(CanvasItem node)
    {
        Transform2D t = node.GetGlobalTransform();
        float sx = Mathf.Abs(t.Scale.X);
        float sy = Mathf.Abs(t.Scale.Y);
        float unit = 1f;
        if (sx > 0.01f) unit = 1f / sx;
        if (sy > 0.01f && Mathf.Abs(sy - sx) > 0.01f) unit = Mathf.Min(unit, 1f / sy);
        return unit;
    }

    private static void Attach(OfficialFxInstance fx, NCreature node, float unit)
    {
        fx.Position = node.MakeCanvasPositionLocal(node.VfxSpawnPosition);
        fx.Scale = new Vector2(unit, unit);
        node.AddChildSafely(fx);
    }

    private static readonly System.Collections.Generic.Dictionary<ulong, Texture2D> BlurCache = new();

    /// <summary>
    /// 模拟 bloom 用的溢光贴图：只保留「形状 + 平均颜色」，把细噪点整个模糊掉。
    /// 如果直接模糊原图，flow_75 的彩虹噪点会带着颜色溢出十字轮廓，看起来像脏边。
    /// </summary>
    public static Texture2D Blurred(Texture2D src)
    {
        ulong key = (ulong)src.GetInstanceId();
        if (BlurCache.TryGetValue(key, out Texture2D? cached)) return cached;

        Image img = src.GetImage();
        if (img.IsCompressed()) img.Decompress();
        int W = img.GetWidth();
        int H = img.GetHeight();

        // 按 alpha 加权求平均色，让光晕带一点本体色调（十字是彩虹白、烟是灰）
        float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color c = img.GetPixel(x, y);
                sr += c.R * c.A;
                sg += c.G * c.A;
                sb += c.B * c.A;
                sa += c.A;
            }
        }

        Color mean = sa > 1e-4f ? new Color(sr / sa, sg / sa, sb / sa, 1f) : Colors.White;
        var halo = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                halo.SetPixel(x, y, new Color(mean.R, mean.G, mean.B, img.GetPixel(x, y).A));
            }
        }

        int w = Mathf.Max(1, W / 6);
        int h = Mathf.Max(1, H / 6);
        for (int i = 0; i < 2; i++)
        {
            halo.Resize(w, h, Image.Interpolation.Bilinear);
            halo.Resize(W, H, Image.Interpolation.Bilinear);
        }

        var tex = ImageTexture.CreateFromImage(halo);
        BlurCache[key] = tex;
        return tex;
    }

    internal static Texture2D? LoadTexture(string name)
    {
        if (Textures.TryGetValue(name, out Texture2D? cached) && GodotObject.IsInstanceValid(cached)) return cached;

        string path = FxDir + name + ".png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[pluma][OfficialFx] 缺少特效贴图：{path}");
            return null;
        }

        var tex = GD.Load<Texture2D>(path);
        Textures[name] = tex;
        return tex;
    }

    /// <summary>
    /// 复刻 Torappu 的两种粒子材质：
    ///   AlphaBlend  → 2 × 顶点色 × _TintColor × 主贴图，普通混合
    ///   Dissolve Add→ 同一套颜色再乘溶解遮罩（_Amount / _BorderWidth），叠加混合
    /// </summary>
    internal static Shader LoadShader(bool additive, bool dissolve)
    {
        string key = (additive ? "add" : "mix") + (dissolve ? "_diss" : "");
        if (Shaders.TryGetValue(key, out Shader? cached) && GodotObject.IsInstanceValid(cached)) return cached;

        string mode = additive ? "blend_add" : "blend_mix";
        string body = dissolve
            ? """
            uniform sampler2D u_diss : source_color, filter_linear, repeat_enable;
            uniform vec4 u_uv_diss = vec4(1.0);
            uniform float u_amount = 0.25;
            uniform float u_border = 0.5;
            uniform float u_gain = 1.0;

            void fragment() {
                vec2 fuv = u_frame_rect.xy + UV * u_frame_rect.zw;
                vec4 c = texture(u_main, fuv * u_uv_main.xy + u_uv_main.zw + vec2(TIME * u_scroll, 0.0)) * COLOR;
                c.rgb *= u_tint.rgb * 2.0 * u_rgb_gain;
                c.a = clamp(c.a * u_tint.a * 2.0, 0.0, 1.0);

                // 官方 dissolve：m 是「遮罩里还剩多少」，叠加混合所以 rgb 也要乘上它
                float d = texture(u_diss, fuv * u_uv_diss.xy + u_uv_diss.zw).r;
                float r = 1.0 - round(u_amount + 0.5);
                float m = clamp((d - u_amount + u_border * r) / max(u_border, 0.0001), 0.0, 1.0);
                c.a = clamp(c.a * m * u_gain, 0.0, 1.0);

                // 叠加混合：c 里已经含官方的 ×2，这里只需预乘 alpha（等价于 SrcAlpha, One）
                COLOR = vec4(c.rgb * c.a, 1.0);
            }
            """
            : """
            uniform float u_gain = 1.0;

            void fragment() {
                vec2 fuv = u_frame_rect.xy + UV * u_frame_rect.zw;
                vec4 c = texture(u_main, fuv * u_uv_main.xy + u_uv_main.zw + vec2(TIME * u_scroll, 0.0)) * COLOR;
                c.rgb *= u_tint.rgb * 2.0 * u_rgb_gain;
                c.a = clamp(c.a * u_tint.a * 2.0 * u_gain, 0.0, 1.0);
                COLOR = c;
            }
            """;

        string code =
            "shader_type canvas_item;\n" +
            $"render_mode {mode}, unshaded;\n" +
            "uniform sampler2D u_main : source_color, filter_linear, repeat_enable;\n" +
            "uniform vec4 u_uv_main = vec4(1.0, 1.0, 0.0, 0.0);\n" +
            "uniform vec4 u_frame_rect = vec4(0.0, 0.0, 1.0, 1.0);\n" +
            "uniform vec4 u_tint : source_color = vec4(1.0);\n" +
            "uniform float u_scroll = 0.0;\n" +
            body;

        var shader = new Shader { Code = code };
        Shaders[key] = shader;
        return shader;
    }

    private static SpriteFrames? MaxStackFrames()
    {
        if (_maxStackFrames != null && GodotObject.IsInstanceValid(_maxStackFrames)) return _maxStackFrames;

        const int frames = 30;
        const int cols = 8;
        const int cell = 256;
        string path = SheetDir + "max_stack.png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[pluma][OfficialFx] 缺少满层特效精灵表：{path}");
            return null;
        }

        var atlas = GD.Load<Texture2D>(path);
        var sf = new SpriteFrames();
        sf.RemoveAnimation("default");
        sf.AddAnimation("default");
        sf.SetAnimationSpeed("default", 30f);
        sf.SetAnimationLoop("default", false);

        for (int i = 0; i < frames; i++)
        {
            sf.AddFrame("default", new AtlasTexture
            {
                Atlas = atlas,
                Region = new Rect2(i % cols * cell, i / cols * cell, cell, cell),
            });
        }

        _maxStackFrames = sf;
        return sf;
    }
}
