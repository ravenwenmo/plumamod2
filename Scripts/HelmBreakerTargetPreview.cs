using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Pluma.Scripts;

// 兜割选目标时的纯客户端视觉状态：记录本机当前正在用兜割瞄准的敌人。
// 仅供血条预测（OpenWoundPower.GetHealthBarForecastSegments）读取，
// 不写入任何游戏状态，多人模式下不需要同步。
internal static class HelmBreakerTargetPreview
{
    public static Creature? CurrentTarget { get; private set; }

    public static bool IsAimingAt(Creature? creature)
    {
        return ReferenceEquals(CurrentTarget, creature);
    }

    public static void SetTarget(Creature? target)
    {
        if (ReferenceEquals(CurrentTarget, target)) return;

        Creature? previous = CurrentTarget;
        CurrentTarget = target;
        // 瞄准/移开目标时立即刷新受影响敌人的血条预测
        RefreshHealthBar(previous);
        RefreshHealthBar(target);
    }

    private static void RefreshHealthBar(Creature? creature)
    {
        if (creature == null || NCombatRoom.Instance?.GetCreatureNode(creature) is not { } creatureNode)
            return;
        // "%HealthBar" 唯一名在 NCreature 场景下先解析到 NCreatureStateDisplay，
        // 在其场景内再解析到真正的 NHealthBar。
        if (creatureNode.GetNodeOrNull("%HealthBar") is not NCreatureStateDisplay stateDisplay)
            return;
        if (stateDisplay.GetNodeOrNull("%HealthBar") is NHealthBar healthBar)
            healthBar.RefreshValues();
    }
}
