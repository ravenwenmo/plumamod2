using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Pluma.Scripts;

/// <summary>
/// 基酒/鸡尾酒牌打出时，按所选目标的阵营确定执行哪条效果分支。
/// 分支只由游戏状态（目标与持有者）决定，不依赖本地右键模式，保证多人同步。
/// </summary>
public enum SpiritTargetBranch
{
    Self,   // 目标是自己
    Enemy,  // 目标是敌人
    Ally    // 目标是友方玩家
}

public static class SpiritTargeting
{
    /// <summary>
    /// 根据所选目标与持有者的关系解析效果分支：
    /// 目标为空（防御性回退）或等于持有者 → Self；目标是敌人 → Enemy；否则（其他玩家）→ Ally。
    /// </summary>
    public static SpiritTargetBranch Resolve(Creature? target, Creature owner)
    {
        if (target == null || target == owner)
            return SpiritTargetBranch.Self;
        return target.IsEnemy ? SpiritTargetBranch.Enemy : SpiritTargetBranch.Ally;
    }
}
