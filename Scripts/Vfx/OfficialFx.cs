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
    /// <summary>1 个 Unity 单位（米）对应的像素数；按干员身高 ≈1.6m ≈ 190px 取。</summary>
    internal const float PxPerUnit = 118f;

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

        var fx = new OfficialFxInstance { Name = "PlumaFlowAura", ZIndex = 35 };
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
            SpawnY = 0.85f,
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
            SpawnY = 0.85f,
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

        var fx = new OfficialFxInstance { Name = "PlumaHealFx", ZIndex = 40 };
        float unit = UnitScale(node);

        // baoshan1：flow_75 主体 + mask_30 溶解遮罩，Additive；单颗、原地放大再收缩，Roll 30°。
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
            SizeXMax = 0.8f,
            SizeYMin = 0.8f,
            SizeYMax = 0.8f,
            SharedSize = true,
            MaxSize = 1f,
            // SizeModule：scalar 1.5 × 曲线（separateAxes=false，两轴共用同一条）
            SizeXKeys = new[] { new Vector2(0f, 0.509148f), new Vector2(0.276305f, 1.5f), new Vector2(1f, 0.835327f) },
            SizeYKeys = new[] { new Vector2(0f, 0.509148f), new Vector2(0.276305f, 1.5f), new Vector2(1f, 0.835327f) },
            AlphaKeys = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.179416f, 1f),
                new Vector2(0.441184f, 0.627451f), new Vector2(1f, 0f),
            },
            StartColor = new Color(0.352941f, 0.352941f, 0.352941f, 0.27451f),
            Stretch = false,
            RotationDeg = 30f,
            SpawnY = 0.45f,
            Order = 0,
        });

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
            Stretch = false,
            RotationDeg = 90f,
            SpawnHalfWidth = 0.1f,
            SpawnHalfHeight = 0.2f,
            SpawnY = 0.5f,
            SpreadDeg = 35f,
            Order = 1,
        });

        Attach(fx, node, unit);
        fx.PlayOnce(1.6f);
    }

    /// <summary>渐入佳境首次叠满 12 层：kuohuan 网格扩散环（buff_03，仍是旧烘焙）。</summary>
    public static void PlayMaxStack(Creature? owner)
    {
        if (TestMode.IsOn) return;

        NCreature? node = owner?.GetCreatureNode();
        if (node == null) return;

        SpriteFrames? frames = MaxStackFrames();
        if (frames == null) return;

        var root = new Node2D
        {
            Name = "PlumaMaxStackFx",
            ZIndex = 40,
            Position = node.MakeCanvasPositionLocal(node.VfxSpawnPosition),
        };

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "default",
            Centered = true,
            Scale = new Vector2(2.2f, 2.2f),
            Position = new Vector2(0f, -256f * 0.6f),
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

            void fragment() {
                vec4 c = texture(u_main, UV * u_uv_main.xy + u_uv_main.zw + vec2(TIME * u_scroll, 0.0)) * COLOR;
                c.rgb *= u_tint.rgb * 2.0;
                c.a = clamp(c.a * u_tint.a * 2.0, 0.0, 1.0);

                // 官方 dissolve：m 是「遮罩里还剩多少」，叠加混合所以 rgb 也要乘上它
                float d = texture(u_diss, UV * u_uv_diss.xy + u_uv_diss.zw).r;
                float r = 1.0 - round(u_amount + 0.5);
                float m = clamp((d - u_amount + u_border * r) / max(u_border, 0.0001), 0.0, 1.0);
                c.a *= m;

                // 叠加混合：rgb 预先乘上 alpha，输出 alpha=1，等价于 dst + src
                COLOR = vec4(c.rgb * c.a * 2.0, 1.0);
            }
            """
            : """
            void fragment() {
                vec4 c = texture(u_main, UV * u_uv_main.xy + u_uv_main.zw + vec2(TIME * u_scroll, 0.0)) * COLOR;
                c.rgb *= u_tint.rgb * 2.0;
                c.a = clamp(c.a * u_tint.a * 2.0, 0.0, 1.0);
                COLOR = c;
            }
            """;

        string code =
            "shader_type canvas_item;\n" +
            $"render_mode {mode}, unshaded;\n" +
            "uniform sampler2D u_main : source_color, filter_linear, repeat_enable;\n" +
            "uniform vec4 u_uv_main = vec4(1.0, 1.0, 0.0, 0.0);\n" +
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
