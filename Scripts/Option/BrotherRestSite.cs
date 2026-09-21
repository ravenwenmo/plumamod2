using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts.Option;

// 羽毛笔在火堆的“休息”对象是龙舌兰（休息选项见 HealBrotherOption）：
// 羽毛笔自身既回不了血、也不再吃这些休息加成，原版挂在“休息”上的效果一律改成落在龙舌兰身上。
// 这里集中放“原版效果 → 龙舌兰”的换算逻辑，供休息选项与补丁（Scripts/Patch）调用。
public static class BrotherRestSite
{
    // 该玩家的“休息对象”是不是龙舌兰：拿到龙舌兰遗物即是
    // （ReaperBadge 每次进房间自动获取，所以羽毛笔开局第一个火堆就有）。
    public static bool RestsWithBrother(Player? player)
    {
        return player != null && player.GetRelic<BrotherRelic>() != null;
    }

    // 给龙舌兰加最大生命值。对应原版 CreatureCmd.GainMaxHp 的语义：
    // 最大生命 +amount，同时把当前生命也 +amount（不超过上限）。
    // 龙舌兰的战斗外数据是 BrotherStateData（SavedHp/SavedMaxHp），
    // 下次进战斗时 Brother.cs 会按这份数据 SetMaxHp，所以这里改数据即可。
    public static void GainMaxHp(Player player, int amount)
    {
        if (amount <= 0 || !RestsWithBrother(player)) return;

        int newMaxHp = BrotherStateData.GetMaxHp(player) + amount;
        int newHp = Math.Min(BrotherStateData.GetHp(player) + amount, newMaxHp);
        BrotherStateData.SetHp(player, newHp, newMaxHp);   // 内部会刷新龙舌兰遗物的显示
    }

    // 龙舌兰的休息回血量 = 基数（60% 龙舌兰最大生命）+ 原版“休息回血修正”类遗物的加成。
    // 帝王枕头（休息回血 +15）这类“在原数值上加”的修正会自然生效；
    // 羽毛笔自己的 ReaperBadge/AquaDawn 必须跳过：它们的“归零”是针对羽毛笔自身的，
    // 不该让龙舌兰也跟着回不了血（cf0054e 当初就是踩到这个才把修饰符整个去掉的）。
    public static decimal ModifyRestHealAmount(Player player, decimal amount)
    {
        decimal result = amount;
        foreach (RelicModel relic in player.Relics)
        {
            if (relic is ReaperBadge || relic is AquaDawn) continue;
            result = relic.ModifyRestSiteHealAmount(player.Creature, result);
        }
        return Math.Max(0m, result);
    }
}
