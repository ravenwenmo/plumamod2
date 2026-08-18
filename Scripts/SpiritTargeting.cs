using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

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
    /// 目标为空（防御性回退）或等于持有者 → Self；目标是敌人 → Enemy；否则（其他玩家、宠物）→ Ally。
    /// </summary>
    public static SpiritTargetBranch Resolve(Creature? target, Creature owner)
    {
        if (target == null || target == owner)
            return SpiritTargetBranch.Self;
        return target.IsEnemy ? SpiritTargetBranch.Enemy : SpiritTargetBranch.Ally;
    }

    /// <summary>
    /// 目标为宠物（如龙舌兰）时，玩家专属增益（抽牌、能量等）无法生效，
    /// 转为对宠物施加等量力量。返回 true 表示已转换（调用方应跳过原增益）。
    /// </summary>
    public static async Task<bool> ApplyStrengthToPetInstead(
        PlayerChoiceContext choiceContext,
        Creature? target,
        decimal amount,
        Creature applier,
        CardModel source)
    {
        if (target == null || !target.IsPet)
        {
            return false;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, amount, applier, source);
        return true;
    }
}