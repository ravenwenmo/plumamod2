using System.Collections.Generic;
using Godot;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 一次特效实例：挂在目标小人节点下的 Node2D，内部按官方 prefab 的发射器逐帧推进粒子。
/// 实例内部所有长度单位是「像素」，而实例自身被缩放成 1/小人缩放，
/// 因此 prefab 的米制数值可以原样乘 <see cref="OfficialFx.PxPerUnit"/> 使用。
/// </summary>
internal sealed partial class OfficialFxInstance : Node2D
{
    private readonly List<FxEmitter> _emitters = new();
    private float _remaining = -1f;

    /// <summary>1 个方舟 Unity 单位对应多少像素（按小人实际绘制高度换算，见 OfficialFx.MeasurePxPerUnit）。</summary>
    public float PxPerUnit = 118f;


    public void Add(FxEmitter emitter) => _emitters.Add(emitter);

    /// <summary>一次性特效的存活时间；循环光环不调用它，由外部拆节点。</summary>
    public void PlayOnce(float seconds) => _remaining = seconds;

    public override void _Ready()
    {
        foreach (FxEmitter emitter in _emitters) emitter.Build(this);
    }

    public override void _Process(double delta)
    {
        // 卡帧保护：一帧最多推进 50ms，避免掉帧时粒子瞬移
        float dt = Mathf.Min((float)delta, 0.05f);
        foreach (FxEmitter emitter in _emitters) emitter.Tick(dt);

        if (_remaining < 0f) return;
        _remaining -= dt;
        if (_remaining <= 0f) QueueFree();
    }

    private sealed class Particle
    {
        public Vector2 Pos;
        public Vector2 Vel = Vector2.Up;
        public Vector2 Dir = Vector2.Up;
        public float Life = 1f;
        public float Age;
        public float Rx;
        public float Ry;
        public float Roll;
        public float Spin;
        public float Limit = float.MaxValue;
        /// <summary>startColor 是 Unity 的 MinMaxGradient（两色随机）时，这个粒子取到的插值位置。</summary>
        public float ColorT;
        public Sprite2D Sprite = null!;
        /// <summary>模拟 bloom 的光晕层（同一粒子放大 + 模糊后的加色副本）。</summary>
        public Sprite2D? Glow;
        public ShaderMaterial? GlowMat;
        public Texture2D? GlowFrame;
        /// <summary>序列帧发射器每个粒子有独立材质（要各播各的帧），其余发射器共用一份。</summary>
        public ShaderMaterial? Mat;
    }

    /// <summary>单个官方粒子发射器（一个 ParticleSystemRenderer + 它的 modules）。</summary>
    internal sealed class FxEmitter
    {
        // ---- prefab 参数 ----
        public string Name = "";
        public string Main = "";
        public string? Dissolve;
        public Vector4 UvMain = new(1f, 1f, 0f, 0f);
        public Vector4 UvDiss = new(1f, 1f, 0f, 0f);
        public Color Tint = Colors.White;
        public Color StartColor = Colors.White;
        /// <summary>官方 startColor 是 MinMaxGradient（TwoColors）时的第二个颜色，逐粒子随机插值。</summary>
        public Color StartColorB = Colors.White;
        /// <summary>
        /// 亮度补偿：官方在 HDR + bloom 的管线里渲染，StS2 是纯 sRGB 画布，
        /// 细而暗的加色粒子会几乎看不见。这里只放大最终 alpha，不改官方颜色本身。
        /// </summary>
        public float Opacity = 1f;

        /// <summary>
        /// 颜色补偿：官方的 HDR + bloom 会把细亮粒子整体提亮，StS2 没有 bloom，
        /// 只放大 alpha 仍会让「主体是暗噪声」的十字看不清，这里额外放大 rgb。
        /// </summary>
        public float RgbBoost = 1f;

        /// <summary>把主贴图颜色往白里拉（0 = 原色，1 = 纯白）。官方靠 bloom 把十字洗白，这里近似。</summary>
        public float Whitening;

        /// <summary>
        /// 模拟 bloom 的光晕强度：额外画一层放大 + 模糊的加色副本（0 = 关闭）。
        /// 官方管线是 HDR + bloom，亮部会向外溢出；StS2 是 LDR 画布，只能用这层假光晕近似。
        /// </summary>
        public float Glow;

        /// <summary>光晕相对本体的放大倍数。</summary>
        public float GlowScale = 2.2f;
        public float Amount = 0.25f;
        public float Border = 0.5f;
        public float UvScroll;
        public bool Additive = true;

        public bool Loop;
        public float Rate;
        public int Burst;
        public float LifeMin = 0.4f;
        public float LifeMax = 0.5f;
        public float SpeedMin;
        public float SpeedMax;

        /// <summary>官方 ClampVelocity：速度上限（米/秒，0 = 立刻刹停）与每帧衰减比例。</summary>
        public float LimitSpeed = float.MaxValue;

        /// <summary>官方 ClampVelocity 的 dampen，参考实现按「每步」衰减，不是每秒。</summary>
        public float Dampen = 1f;

        /// <summary>官方 UVModule（贴图序列动画）：tilesX / tilesY，0 表示不用序列帧。</summary>
        public int FlipCols;
        public int FlipRows;

        /// <summary>官方 RotationModule：粒子每秒自转（弧度），随机取 [SpinMin, SpinMax]。</summary>
        public float SpinMin;
        public float SpinMax;

        /// <summary>官方 startRotation 随机（0..2π）。</summary>
        public bool RandomRotation;
        public float SizeXMin = 0.1f;
        public float SizeXMax = 0.1f;
        public float SizeYMin = 0.1f;
        public float SizeYMax = 0.1f;
        /// <summary>size3D=false 时两轴共用同一个随机基数（官方 separateAxes 的用法）。</summary>
        public bool SharedSize;
        public Vector2[] SizeXKeys = { new(0f, 1f), new(1f, 1f) };
        public Vector2[] SizeYKeys = { new(0f, 1f), new(1f, 1f) };
        public Vector2[] AlphaKeys = { new(0f, 1f), new(1f, 0f) };
        /// <summary>官方 renderer 的 m_MaxParticleSize（米）。</summary>
        public float MaxSize = 5f;
        public bool Stretch = true;
        public float LengthScale = 2f;
        public float VelocityScale;
        public float SpawnHalfWidth;
        public float SpawnHalfHeight;
        /// <summary>官方粒子系统节点相对「脚底」的高度（米，向上为正）。</summary>
        public float NodeYUp;
        /// <summary>贴图左右翻转：官方 trail 的箭头纹理朝后，翻过来才和运动方向一致。</summary>
        public bool FlipX;
        public float SpreadDeg;
        public float RotationDeg;
        public float Prewarm;
        public int Order;
        public int MaxParticles = 64;

        // ---- 运行时 ----
        private OfficialFxInstance _host = null!;
        private Texture2D _tex = null!;
        private Texture2D? _baked;
        private Texture2D[] _frames = System.Array.Empty<Texture2D>();
        private Texture2D? _diss;
        private ShaderMaterial _mat = null!;
        private float _emitAcc;
        private bool _burstDone;
        private readonly List<Particle> _live = new();
        private readonly Stack<Sprite2D> _free = new();
        private readonly Stack<Sprite2D> _freeGlow = new();
        private readonly RandomNumberGenerator _rng = new();

        public void Build(OfficialFxInstance host)
        {
            _host = host;
            Texture2D? tex = OfficialFx.LoadTexture(Main);
            if (tex == null)
            {
                GD.PushWarning($"[pluma][OfficialFx] 发射器 {Name} 无贴图，已跳过");
                return;
            }

            // 溶解遮罩不能依赖运行时采样：Godot 的 sampler2D 只要没绑上就返回白色，
            // 结果就是「整块方形」而不是十字/雾团。这里在加载时把遮罩直接烘进主贴图的 alpha。
            _diss = Dissolve != null ? OfficialFx.LoadTexture(Dissolve) : null;
            if (Dissolve != null && _diss == null)
            {
                // 遮罩缺失时宁可不画：否则 flow_75 的 alpha 全是 1，会画出一整块彩色方块
                GD.PushWarning($"[pluma][OfficialFx] 发射器 {Name} 的遮罩 {Dissolve} 丢失，已跳过");
                return;
            }
            if (_diss != null)
            {
                _baked = BakeDissolve(tex, _diss, Amount, Border, UvMain, UvDiss, Whitening);
                _tex = _baked;
                UvMain = new Vector4(1f, 1f, 0f, 0f);
                // UVTween 原本是让「主贴图采样」随时间滚动；烘焙后主贴图已经是裁好形状的成品，
                // 再滚动就会把十字/雾团整个卷出轮廓（repeat_enable 还会重复），所以这里必须清零。
                UvScroll = 0f;
                _diss = null;
            }
            else
            {
                _tex = tex;
            }

            if (FlipCols > 1 && FlipRows > 1) _frames = SliceFrames(_tex);
            _mat = MakeMaterial();
            _rng.Randomize();

            // prewarm：官方勾了 prewarm，开战时光环就已经铺满，不会从空开始
            if (Prewarm > 0f && Loop && Rate > 0f)
            {
                int count = Mathf.CeilToInt(Rate * LifeMax);
                for (int i = 0; i < count; i++) Spawn(_rng.RandfRange(0f, LifeMax));
            }
        }

        /// <summary>
        /// 把 Torappu 的溶解遮罩烘进贴图 alpha：
        ///   m = clamp((mask.r - amount + border * r) / border)，r = 1 - round(amount + 0.5)
        /// 输出 rgb 保持主贴图不变（官方叠加时靠 ×2 和 bloom 提亮），alpha = 主贴图 a × m。
        /// </summary>
        private static Texture2D BakeDissolve(Texture2D main, Texture2D mask, float amount, float border, Vector4 uvMain, Vector4 uvDiss, float whiten)
        {
            Image src = main.GetImage();
            Image cut = mask.GetImage();
            int w = cut.GetWidth();
            int h = cut.GetHeight();
            var outImg = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            float rr = 1f - Mathf.Round(amount + 0.5f);
            float inv = 1f / Mathf.Max(border, 1e-4f);
            float outsideMax = 0f;

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    // 主贴图按官方 uv 平铺采样（等价于 shader 里的 uv * uvMain.xy + uvMain.zw）
                    float su = u * uvMain.X + uvMain.Z;
                    float sv = v * uvMain.Y + uvMain.W;
                    su -= Mathf.Floor(su);
                    sv -= Mathf.Floor(sv);
                    Color c = src.GetPixel(
                        Mathf.Clamp((int)(su * src.GetWidth()), 0, src.GetWidth() - 1),
                        Mathf.Clamp((int)(sv * src.GetHeight()), 0, src.GetHeight() - 1));

                    float du = u * uvDiss.X + uvDiss.Z;
                    float dv = v * uvDiss.Y + uvDiss.W;
                    du -= Mathf.Floor(du);
                    dv -= Mathf.Floor(dv);
                    float d = cut.GetPixel(
                        Mathf.Clamp((int)(du * cut.GetWidth()), 0, cut.GetWidth() - 1),
                        Mathf.Clamp((int)(dv * cut.GetHeight()), 0, cut.GetHeight() - 1)).R;
                    float m = Mathf.Clamp((d - amount + border * rr) * inv, 0f, 1f);
                    var rgb = new Color(
                        Mathf.Lerp(c.R, 1f, whiten),
                        Mathf.Lerp(c.G, 1f, whiten),
                        Mathf.Lerp(c.B, 1f, whiten));
                    float outA = c.A * m;
                    if (d < 0.02f) outsideMax = Mathf.Max(outsideMax, outA);
                    outImg.SetPixel(x, y, new Color(rgb.R, rgb.G, rgb.B, outA));
                }
            }

            GD.Print($"[pluma][OfficialFx] 烘焙 {main.ResourcePath} × {mask.ResourcePath}：遮罩外 alpha 最大值 = {outsideMax:0.000}");
            return ImageTexture.CreateFromImage(outImg);
        }

        /// <summary>把序列帧图集按 tiles 切成独立贴图（Unity 的 UV 原点在左下，PNG 在左上，行要翻过来）。</summary>
        private Texture2D[] SliceFrames(Texture2D atlas)
        {
            Image img = atlas.GetImage();
            int cw = img.GetWidth() / FlipCols;
            int ch = img.GetHeight() / FlipRows;
            var frames = new Texture2D[FlipCols * FlipRows];
            for (int row = 0; row < FlipRows; row++)
            {
                int y = (FlipRows - 1 - row) * ch;
                for (int col = 0; col < FlipCols; col++)
                {
                    Image cell = img.GetRegion(new Rect2I(col * cw, y, cw, ch));
                    frames[row * FlipCols + col] = ImageTexture.CreateFromImage(cell);
                }
            }

            return frames;
        }

        /// <summary>按当前 prefab 参数新建一份材质（序列帧粒子需要每粒子一份）。</summary>
        private ShaderMaterial MakeMaterial()
        {
            var mat = new ShaderMaterial { Shader = OfficialFx.LoadShader(Additive, _diss != null) };
            mat.SetShaderParameter("u_main", _tex);
            mat.SetShaderParameter("u_tint", Tint);
            mat.SetShaderParameter("u_uv_main", UvMain);
            mat.SetShaderParameter("u_uv_diss", UvDiss);
            mat.SetShaderParameter("u_amount", Amount);
            mat.SetShaderParameter("u_border", Border);
            mat.SetShaderParameter("u_scroll", UvScroll);
            mat.SetShaderParameter("u_gain", Opacity);
            mat.SetShaderParameter("u_rgb_gain", RgbBoost);
            if (_diss != null) mat.SetShaderParameter("u_diss", _diss);
            return mat;
        }

        public void Tick(float dt)
        {
            if (_tex == null) return;

            if (Loop)
            {
                if (Rate > 0f)
                {
                    _emitAcc += Rate * dt;
                    while (_emitAcc >= 1f)
                    {
                        _emitAcc -= 1f;
                        Spawn();
                    }
                }
            }
            else if (!_burstDone && Burst > 0)
            {
                _burstDone = true;
                for (int i = 0; i < Burst; i++) Spawn();
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Particle p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Sprite.Visible = false;
                    _free.Push(p.Sprite);
                    if (p.Glow != null)
                    {
                        p.Glow.Visible = false;
                        _freeGlow.Push(p.Glow);
                        p.Glow = null;
                    }
                    _live.RemoveAt(i);
                    continue;
                }

                // 官方 ClampVelocity：超过上限时按 dampen 每帧逼近上限（参考实现不乘 dt）
                float sp = p.Vel.Length();
                if (sp > p.Limit && sp > 1e-4f)
                {
                    float k = (sp + (p.Limit - sp) * Dampen) / sp;
                    p.Vel *= k;
                }

                p.Pos += p.Vel * dt;
                p.Roll += p.Spin * dt;
                Update(p);
            }
        }

        private void Spawn(float preAge = 0f)
        {
            if (_tex == null || _live.Count >= MaxParticles) return;

            float life = Mathf.Max(0.05f, _rng.RandfRange(LifeMin, LifeMax));
            var p = new Particle
            {
                Life = life,
                Rx = _rng.Randf(),
                Ry = _rng.Randf(),
                ColorT = _rng.Randf(),
                Roll = RotationDeg + _rng.RandfRange(-6f, 6f),
            };

            float ang = Mathf.DegToRad(SpreadDeg) * _rng.RandfRange(-1f, 1f);
            p.Dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
            p.Vel = p.Dir * (_rng.RandfRange(SpeedMin, SpeedMax) * _host.PxPerUnit);
            p.Limit = LimitSpeed * _host.PxPerUnit;

            // 锚点就是小人的脚底（Visuals 的 %CenterPos 默认在原点），再按官方节点偏移往上抬
            float x = _rng.RandfRange(-SpawnHalfWidth, SpawnHalfWidth) * _host.PxPerUnit;
            float y = -NodeYUp * _host.PxPerUnit
                      + _rng.RandfRange(-SpawnHalfHeight, SpawnHalfHeight) * _host.PxPerUnit;
            p.Pos = new Vector2(x, y);

            if (RandomRotation) p.Roll = _rng.RandfRange(0f, Mathf.Tau);
            p.Spin = _rng.RandfRange(SpinMin, SpinMax);

            p.Age = Mathf.Clamp(preAge, 0f, life * 0.95f);
            p.Pos += p.Vel * p.Age;

            Sprite2D sprite = _free.Count > 0 ? _free.Pop() : NewSprite();
            sprite.Visible = true;
            p.Sprite = sprite;
            _live.Add(p);
            Update(p);
        }

        private Sprite2D NewSprite()
        {
            var sprite = new Sprite2D
            {
                Texture = _tex,
                Material = _mat,
                Centered = true,
                FlipH = FlipX,
                ZIndex = Order,
                ZAsRelative = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            };
            _host.AddChild(sprite);
            return sprite;
        }

        private Sprite2D NewGlow()
        {
            var sprite = new Sprite2D
            {
                Centered = true,
                FlipH = FlipX,
                ZAsRelative = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            };
            _host.AddChild(sprite);
            return sprite;
        }

        private void Update(Particle p)
        {
            float t = Mathf.Clamp(p.Age / p.Life, 0f, 1f);
            float baseX = Mathf.Lerp(SizeXMin, SizeXMax, p.Rx);
            float baseY = SharedSize ? baseX : Mathf.Lerp(SizeYMin, SizeYMax, p.Ry);

            float w = Mathf.Min(baseX * Eval(SizeXKeys, t), MaxSize) * _host.PxPerUnit;
            float h = Mathf.Min(baseY * Eval(SizeYKeys, t), MaxSize) * _host.PxPerUnit;
            float alpha = Mathf.Clamp(Eval(AlphaKeys, t), 0f, 1f);

            Sprite2D sprite = p.Sprite;
            // 官方 startColor 是 MinMaxGradient（TwoColors）：每个粒子在 A/B 之间随机取一个颜色
            Color col = StartColor.Lerp(StartColorB, p.ColorT);
            sprite.Modulate = new Color(col.R, col.G, col.B, Mathf.Clamp(col.A * alpha, 0f, 1f));

            // 官方 UVModule：整段生命周期均匀播一遍贴图序列（timeMode 0 / frameOverTime 0→1）
            if (FlipCols > 1 && FlipRows > 1) ApplyFrame(p, t);

            Vector2 texSize = _frames.Length > 0 ? _frames[0].GetSize() : _tex.GetSize();
            if (Stretch)
            {
                // 官方 stretch：长度 = sizeY × lengthScale + 速度 × velocityScale，粒子在「头」端
                float speed = p.Vel.Length();
                float len = Mathf.Max(1f, h * LengthScale + speed * VelocityScale);
                float width = Mathf.Max(1f, w);
                sprite.Rotation = p.Dir.Angle();
                sprite.Position = p.Pos - p.Dir * (len * 0.5f);
                sprite.Scale = new Vector2(len / texSize.X, width / texSize.Y);
            }
            else
            {
                sprite.Rotation = Mathf.DegToRad(p.Roll);
                sprite.Position = p.Pos;
                sprite.Scale = new Vector2(
                    Mathf.Max(1f, w) / texSize.X,
                    Mathf.Max(1f, h) / texSize.Y);
            }

            // 假 bloom：同一粒子再画一层「放大 + 模糊」的加色副本，模拟官方 HDR 管线的溢光
            if (Glow > 0f)
            {
                Sprite2D? glow = p.Glow;
                if (glow == null)
                {
                    glow = _freeGlow.Count > 0 ? _freeGlow.Pop() : NewGlow();
                    glow.Visible = true;
                    p.Glow = glow;
                    p.GlowMat = null;
                    p.GlowFrame = null;
                }

                glow.Position = sprite.Position;
                glow.Rotation = sprite.Rotation;
                glow.Scale = sprite.Scale * GlowScale;
                glow.ZIndex = Order - 1;
                if (!ReferenceEquals(p.GlowFrame, sprite.Texture))
                {
                    p.GlowFrame = sprite.Texture;
                    p.GlowMat ??= MakeMaterial();
                    p.GlowMat.SetShaderParameter("u_main", OfficialFx.Blurred(sprite.Texture));
                }
                if (!ReferenceEquals(glow.Material, p.GlowMat)) glow.Material = p.GlowMat;

                Color gc = sprite.Modulate;
                glow.Modulate = new Color(gc.R, gc.G, gc.B, Mathf.Clamp(gc.A * Glow, 0f, 1f));
            }
        }

        /// <summary>
        /// 序列帧：官方 UVModule 每帧换一张子图。这里在加载时把图集切成独立贴图，
        /// 每个粒子用自己的一份材质（共用材质的话所有粒子只能停在同一帧）。
        /// </summary>
        private void ApplyFrame(Particle p, float t)
        {
            int index = Mathf.Clamp((int)(Mathf.Min(t, 0.9999f) * _frames.Length), 0, _frames.Length - 1);
            Texture2D frame = _frames[index];
            p.Mat ??= MakeMaterial();
            if (!ReferenceEquals(p.Sprite.Material, p.Mat)) p.Sprite.Material = p.Mat;
            if (!ReferenceEquals(p.Sprite.Texture, frame))
            {
                p.Sprite.Texture = frame;
                p.Mat.SetShaderParameter("u_main", frame);
            }
        }

        private static float Eval(Vector2[] keys, float t)
        {
            if (keys.Length == 0) return 1f;
            if (t <= keys[0].X) return keys[0].Y;

            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].X) continue;
                float k = Mathf.InverseLerp(keys[i - 1].X, keys[i].X, t);
                return Mathf.Lerp(keys[i - 1].Y, keys[i].Y, k);
            }

            return keys[^1].Y;
        }
    }
}
