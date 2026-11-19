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
        public Sprite2D Sprite = null!;
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
        public float SpawnY;
        public float SpreadDeg;
        public float RotationDeg;
        public float Prewarm;
        public int Order;
        public int MaxParticles = 64;

        // ---- 运行时 ----
        private Node2D _host = null!;
        private Texture2D _tex = null!;
        private ShaderMaterial _mat = null!;
        private float _emitAcc;
        private bool _burstDone;
        private readonly List<Particle> _live = new();
        private readonly Stack<Sprite2D> _free = new();
        private readonly RandomNumberGenerator _rng = new();

        public void Build(Node2D host)
        {
            _host = host;
            Texture2D? tex = OfficialFx.LoadTexture(Main);
            if (tex == null)
            {
                GD.PushWarning($"[pluma][OfficialFx] 发射器 {Name} 无贴图，已跳过");
                return;
            }

            _tex = tex;
            Texture2D? diss = Dissolve != null ? OfficialFx.LoadTexture(Dissolve) : null;
            _mat = new ShaderMaterial { Shader = OfficialFx.LoadShader(Additive, diss != null) };
            _mat.SetShaderParameter("u_main", _tex);
            _mat.SetShaderParameter("u_tint", Tint);
            _mat.SetShaderParameter("u_uv_main", UvMain);
            _mat.SetShaderParameter("u_uv_diss", UvDiss);
            _mat.SetShaderParameter("u_amount", Amount);
            _mat.SetShaderParameter("u_border", Border);
            _mat.SetShaderParameter("u_scroll", UvScroll);
            if (diss != null) _mat.SetShaderParameter("u_diss", diss);

            _rng.Randomize();

            // prewarm：官方勾了 prewarm，开战时光环就已经铺满，不会从空开始
            if (Prewarm > 0f && Loop && Rate > 0f)
            {
                int count = Mathf.CeilToInt(Rate * LifeMax);
                for (int i = 0; i < count; i++) Spawn(_rng.RandfRange(0f, LifeMax));
            }
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
                    _live.RemoveAt(i);
                    continue;
                }

                p.Pos += p.Vel * dt;
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
                Roll = RotationDeg + _rng.RandfRange(-6f, 6f),
            };

            float ang = Mathf.DegToRad(SpreadDeg) * _rng.RandfRange(-1f, 1f);
            p.Dir = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
            p.Vel = p.Dir * (_rng.RandfRange(SpeedMin, SpeedMax) * OfficialFx.PxPerUnit);

            float x = _rng.RandfRange(-SpawnHalfWidth, SpawnHalfWidth) * OfficialFx.PxPerUnit;
            float y = SpawnY * OfficialFx.PxPerUnit
                      + _rng.RandfRange(-SpawnHalfHeight, SpawnHalfHeight) * OfficialFx.PxPerUnit;
            p.Pos = new Vector2(x, y);

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
                ZIndex = Order,
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

            float w = Mathf.Min(baseX * Eval(SizeXKeys, t), MaxSize) * OfficialFx.PxPerUnit;
            float h = Mathf.Min(baseY * Eval(SizeYKeys, t), MaxSize) * OfficialFx.PxPerUnit;
            float alpha = Mathf.Clamp(Eval(AlphaKeys, t), 0f, 1f);

            Sprite2D sprite = p.Sprite;
            sprite.Modulate = new Color(StartColor.R, StartColor.G, StartColor.B, StartColor.A * alpha);

            Vector2 texSize = _tex.GetSize();
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
