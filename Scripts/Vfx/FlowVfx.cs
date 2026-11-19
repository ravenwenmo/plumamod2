using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 羽毛笔被动相关特效的对外入口（薄封装，真正干活的是 <see cref="OfficialFx"/>）。
///
/// 早期版本这里是"用代码手绘贴图 + Tween 拼出来的"自制特效，观感和原版差得远，
/// 现在全部换成明日方舟官方特效 prefab 的离线烘焙结果，见 <see cref="OfficialFx"/> 的注释。
///
///   · <see cref="PlayStackGain"/>：渐入佳境每获得层数时调用（原版 crow_attack_buff_01）
///   · <see cref="PlayHeal"/>：通过特性治疗自身时调用（原版 common_heal_hit_01，染成银白）
/// </summary>
public static class FlowVfx
{
    /// <summary>原版层数上限（满层）。</summary>
    public const int MaxStacks = 12;

    /// <summary>获得渐入佳境层数时调用。<paramref name="gained"/> 是本次增加层数，<paramref name="total"/> 是当前总层数。</summary>
    public static void PlayStackGain(Creature? owner, int gained, int total)
    {
        OfficialFx.PlayStackGain(owner, gained, total);
    }

    /// <summary>特性治疗自身时调用（银白十字，1.5 倍、底边不动）。</summary>
    public static void PlayHeal(Creature? owner)
    {
        OfficialFx.PlayHeal(owner);
    }
}
