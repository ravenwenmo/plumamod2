using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace Pluma.Scripts;

/// <summary>
/// 高速切割层数的 run 内持久化：
/// 层数变化实时写入 per-player run 数据槽，每次战斗开始时从槽位恢复能力，
/// 实现层数在当前 run 内跨战斗保留，并在战斗外存档/读档后恢复。
/// 战斗结束清能力走的是静默路径（RemoveInternal，不触发 AfterRemoved），
/// 因此槽位会保留最后一次层数；新 run 槽位为空，默认 0 层。
/// </summary>
[RegisterSingleton]
public class RapidSlashingStacksPersistence : HookedSingletonModel
{
    public RapidSlashingStacksPersistence() : base(HookType.Combat)
    {
    }

    /// <summary>
    /// 战斗开始时为每位玩家恢复上次战斗留下的层数。
    /// 与原版遗物在 BeforeCombatStart 里直接执行命令的做法一致：
    /// 此时没有玩家选择上下文，使用 ThrowingPlayerChoiceContext。
    /// </summary>
    public override async Task BeforeCombatStart()
    {
        if (CurrentCombatState == null || Entry.RapidSlashingStacksData == null)
        {
            return;
        }

        foreach (Player player in CurrentCombatState.Players)
        {
            try
            {
                if (player.Creature == null || player.Creature.HasPower<RapidSlashingStacks>())
                {
                    continue;
                }

                int count = Entry.RapidSlashingStacksData.Get(player).Count;
                if (count <= 0)
                {
                    continue;
                }

                await PowerCmd.Apply<RapidSlashingStacks>(
                    new ThrowingPlayerChoiceContext(), player.Creature, count, null, null, silent: true);
            }
            catch (Exception e)
            {
                Entry.Logger.Error($"[pluma] 恢复高速切割层数失败: {e}");
            }
        }
    }

    /// <summary>层数变化（首次施加/叠层/削减）后把当前层数写入 run 数据槽。</summary>
    public static void Sync(PowerModel power)
    {
        if (Entry.RapidSlashingStacksData == null || power.Owner?.Player is not Player player)
        {
            return;
        }

        try
        {
            Entry.RapidSlashingStacksData.Modify(player, data => data.Count = power.Amount);
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[pluma] 同步高速切割层数失败: {e}");
        }
    }

    /// <summary>能力被正常移除（如达到阈值被消耗）时把槽位层数清零。</summary>
    public static void Clear(Creature oldOwner)
    {
        if (Entry.RapidSlashingStacksData == null || oldOwner?.Player is not Player player)
        {
            return;
        }

        try
        {
            Entry.RapidSlashingStacksData.Modify(player, data => data.Count = 0);
        }
        catch (Exception e)
        {
            Entry.Logger.Error($"[pluma] 清零高速切割层数失败: {e}");
        }
    }
}

/// <summary>
/// 高速切割层数 run 数据槽的载荷（满足 PlayerRunSavedData 的 class 约束）。
/// </summary>
public class RapidSlashingStacksSave
{
    public int Count { get; set; }
}
