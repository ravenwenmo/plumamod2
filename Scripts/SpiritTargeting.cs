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
    /// 注意：这是实际执行逻辑使用的解析。友方玩家与宠物都归入 Ally，
    /// 各卡牌在 Ally 分支内再通过 ApplyXxxToPetInstead 区分两者。
    /// </summary>
    public static SpiritTargetBranch Resolve(Creature? target, Creature owner)
    {
        if (target == null || target == owner)
            return SpiritTargetBranch.Self;
        return target.IsEnemy ? SpiritTargetBranch.Enemy : SpiritTargetBranch.Ally;
    }

    /// <summary>
    /// 仅用于本地显示（卡牌描述预览、发光颜色）的目标解析：
    /// 宠物（龙舌兰）→ Ally；自己或友方玩家 → Self（两者玩家效果一致）；敌人 → Enemy。
    /// 不要用这个解析结果驱动实际效果，实际执行请继续使用 <see cref="Resolve"/>。
    /// </summary>
    public static SpiritTargetBranch ResolveForDisplay(Creature? target, Creature owner)
    {
        if (target == null || target == owner)
            return SpiritTargetBranch.Self;
        if (target.IsPet)
            return SpiritTargetBranch.Ally;
        return target.IsEnemy ? SpiritTargetBranch.Enemy : SpiritTargetBranch.Self;
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

    /// <summary>
    /// 目标为宠物时，将玩家专属增益转换为对宠物施加等量再生（Regen）。
    /// </summary>
    public static async Task<bool> ApplyRegenToPetInstead(
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
        await PowerCmd.Apply<RegenPower>(choiceContext, target, amount, applier, source);
        return true;
    }

    /// <summary>
    /// 目标为宠物时，将玩家专属增益转换为对宠物施加等量覆甲（Plating）。
    /// </summary>
    public static async Task<bool> ApplyPlatingToPetInstead(
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
        await PowerCmd.Apply<PlatingPower>(choiceContext, target, amount, applier, source);
        return true;
    }

    /// <summary>
    /// 目标为宠物时，将玩家专属回复效果转换为对宠物治疗等量生命值。
    /// </summary>
    public static async Task<bool> ApplyHealToPetInstead(
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
        await CreatureCmd.Heal(target, amount);
        return true;
    }

    /// <summary>
    /// 目标为宠物时的占位效果：对龙舌兰施加特性（强化循环计数）。
    /// 用于基酒/鸡尾酒牌中尚无龙舌兰特殊效果的 Ally 分支，占位量通常为 25（约等于强化循环一回合的自然积累）。
    /// </summary>
    public static async Task<bool> ApplyTraitToPetInstead(
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
        await PowerCmd.Apply<TraitPower>(choiceContext, target, amount, applier, source);
        return true;
    }
}