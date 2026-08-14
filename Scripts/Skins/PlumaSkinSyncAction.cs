using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace Pluma.Scripts;

/// <summary>
/// 皮肤同步托管网络动作（兜底链路）。
///
/// 主链路是大厅暂存槽（SelectSkinInLobby → RunSavedData 大厅暂存 → 开局 payload 全端导入）。
/// 本动作作为兜底：通过原版动作队列同步器在每局开始时广播一次本地玩家的皮肤，
/// 所有端（含发起方）在动作执行时把皮肤写入发起玩家对应的 RunSavedData 槽位。
/// 执行时不再次发送网络请求，避免循环同步。
/// </summary>
public static class PlumaSkinSyncAction
{
    private static readonly RitsuLibManagedNetActionDescriptor<int> Descriptor = new(
        ModuleId: Entry.ModId,
        ActionKey: "sync_skin_index",
        // 载荷只含皮肤索引一个字节（皮肤数量 < 256）
        Serialize: index => new[] { (byte)index },
        Deserialize: span => span.Length > 0 ? span[0] : 0,
        Execute: ExecuteAsync,
        // Any：不在战斗结束时取消，随时可执行
        ActionType: GameActionType.Any);

    // 已处理过的 RunState（保证每局只写入并广播一次，RunSavedDataPreparing 与 RunInit 补丁可能先后触发）
    private static readonly HashSet<RunState> SyncedRuns = new();

    private static Task ExecuteAsync(RitsuLibManagedNetActionContext<int> context)
    {
        int skinIndex = context.Message;
        var owner = context.Player; // 动作所有者 = 发起皮肤同步的玩家
        if (owner.RunState is RunState runState && Entry.SkinData != null)
        {
            Entry.SkinData.Modify(runState, owner.NetId, wrapper => wrapper.Index = skinIndex);
            GD.Print($"[PlumaSkins] Applying skin {skinIndex} to player {owner.NetId}");
        }
        else
        {
            GD.Print($"[PlumaSkins] Skin sync action executed but slot unavailable for player {owner?.NetId}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 由本地玩家请求一次皮肤广播（每局仅调用一次）。返回 false 表示未能发出请求
    /// （例如对端不支持托管动作、尚未进入局内等），此时依赖大厅暂存链路。
    /// </summary>
    public static bool TrySyncLocalSkin(int skinIndex)
    {
        bool sent = RitsuLibManagedNetActions.Request(RunManager.Instance, Descriptor, skinIndex);
        GD.Print($"[PlumaSkins] TrySyncLocalSkin skin={skinIndex} sent={sent}");
        return sent;
    }

    /// <summary>
    /// 保证本地玩家在该局的皮肤槽位有值并广播一次。
    /// 每局幂等：无论由 RunSavedDataPreparingEvent 还是 RunInit 补丁触发，都只执行一次。
    /// </summary>
    public static void EnsureLocalSkinSynced(RunState runState)
    {
        if (!SyncedRuns.Add(runState))
            return;
        try
        {
            Player? me = null;
            try
            {
                me = LocalContext.GetMe(runState);
            }
            catch
            {
                // NetId 已设置但集合中找不到本地玩家等异常，走回退逻辑
            }
            me ??= runState.Players.FirstOrDefault(p => p.Character is PlumaCharacter);

            if (me == null)
            {
                GD.Print("[PlumaSkins] EnsureLocalSkinSynced: 未找到本地玩家，跳过皮肤处理");
                return;
            }

            if (!Entry.SkinData.TryGet(runState, me.NetId, out _))
            {
                Entry.SkinData.Modify(me, wrapper => wrapper.Index = PlumaSkins.LocalIndex);
                GD.Print($"[PlumaSkins] EnsureLocalSkinSynced: 为本地玩家 {me.NetId} 写入皮肤 {PlumaSkins.LocalIndex}");
            }

            // 广播一次本地玩家的皮肤（大厅暂存链路之外的兜底）
            TrySyncLocalSkin(PlumaSkins.LocalIndex);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[pluma] EnsureLocalSkinSynced 失败: {ex.Message}");
        }
    }
}
