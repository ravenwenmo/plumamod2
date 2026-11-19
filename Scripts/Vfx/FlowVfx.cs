using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 羽毛笔被动相关特效的对外入口（薄封装，真正干活的是 <see cref="OfficialFx"/>）。
///
///   · <see cref="SyncAura"/>：渐入佳境**持有期间**持续播放的循环光环
///     （官方 crow_attack_buff_02 = 天赋 1 攻速叠层的身上特效）
///   · <see cref="PlayHeal"/>：通过特性治疗自身时的一次性白十字闪光
///     （官方 crow_attack_buff_01 = 特性回复特 effect）
///
/// 光环按 Creature 记账：同一场战斗里只挂一份，移除能力或战斗结束时拆掉。
/// </summary>
public static class FlowVfx
{
    /// <summary>原版层数上限（满层）。</summary>
    public const int MaxStacks = 12;

    private static readonly Dictionary<Creature, Node2D> Auras = new();

    /// <summary>按需挂上/拆掉渐入佳境光环。</summary>
    public static void SyncAura(Creature? owner, bool active)
    {
        if (owner == null) return;

        if (active)
        {
            if (Auras.TryGetValue(owner, out Node2D? existing) && GodotObject.IsInstanceValid(existing)) return;
            Node2D? aura = OfficialFx.CreateAura(owner);
            if (aura != null) Auras[owner] = aura;
        }
        else
        {
            if (Auras.TryGetValue(owner, out Node2D? existing))
            {
                if (GodotObject.IsInstanceValid(existing)) existing.QueueFree();
                Auras.Remove(owner);
            }
        }
    }

    /// <summary>兼容旧调用点：拿到层数就等于"光环该在了"。</summary>
    public static void PlayStackGain(Creature? owner, int gained, int total)
    {
        SyncAura(owner, true);
        if (total >= MaxStacks && total - gained < MaxStacks) PlayMaxStack(owner);
    }

    /// <summary>满 12 层的一次性扩散环爆发（只在跨越上限那一次播）。</summary>
    public static void PlayMaxStack(Creature? owner)
    {
        OfficialFx.PlayMaxStack(owner);
    }

    /// <summary>特性治疗自身时调用（白十字爆闪，1.5 倍、底边不动）。</summary>
    public static void PlayHeal(Creature? owner)
    {
        OfficialFx.PlayHeal(owner);
    }
}
