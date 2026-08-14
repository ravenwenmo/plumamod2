using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace Pluma.Scripts;

/// <summary>
/// 追踪当前活跃的 StartRunLobby（由 PlumaStartRunLobbyPatch 在构造/清理时维护），
/// 供皮肤同步代码随时找到当前大厅。做法与 RitsuLib 内部的大厅追踪一致：
/// 用 NetService 作为键，因为 RunManager.Instance.NetService 在进局前后都可用。
/// </summary>
internal static class PlumaLobbyRegistry
{
    private static readonly ConditionalWeakTable<INetGameService, StartRunLobby> LobbyByNetService = new();

    public static void Track(StartRunLobby lobby)
    {
        LobbyByNetService.Remove(lobby.NetService);
        LobbyByNetService.Add(lobby.NetService, lobby);
        GD.Print($"[pluma] PlumaLobbyRegistry.Track: netType={lobby.NetService.Type}");
    }

    public static void Untrack(StartRunLobby lobby)
    {
        LobbyByNetService.Remove(lobby.NetService);
        GD.Print($"[pluma] PlumaLobbyRegistry.Untrack: netType={lobby.NetService.Type}");
    }

    /// <summary>获取当前联机服务对应的大厅；不在大厅中时返回 null。</summary>
    public static StartRunLobby? TryGetCurrent()
    {
        try
        {
            var netService = RunManager.Instance.NetService;
            if (netService != null && LobbyByNetService.TryGetValue(netService, out var lobby))
                return lobby;
        }
        catch
        {
            // RunManager 尚未初始化等场景，忽略
        }
        return null;
    }
}
