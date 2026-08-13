using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace Pluma.Scripts;

public partial class SkinSelector : Control
{
    private Button _leftButton;
    private Button _rightButton;
    private Label _skinNameLabel;

    public override void _Ready()
    {
        _leftButton = GetNode<Button>("VBoxContainer/HBoxContainer/LeftButton");
        _rightButton = GetNode<Button>("VBoxContainer/HBoxContainer/RightButton");
        _skinNameLabel = GetNode<Label>("VBoxContainer/HBoxContainer2/SkinNameLabel");


        _leftButton.MouseFilter = Control.MouseFilterEnum.Stop;
        _rightButton.MouseFilter = Control.MouseFilterEnum.Stop;
        _skinNameLabel.MouseFilter = Control.MouseFilterEnum.Stop;


        _leftButton.Pressed += () => { SelectSkin(0); };
        _rightButton.Pressed += () => { SelectSkin(1); };

        // 进入大厅/角色选择界面时，把本地皮肤推送到联机暂存槽，
        // 保证即使不点击按钮，其他玩家也能看到自己的皮肤
        PushLocalSkinToLobby();

        UpdateSelectedSkin();
    }

    // 按当前环境选择写入方式：多人 → 大厅暂存槽（自动同步），单人 → 本地配置
    private void SelectSkin(int index)
    {
        var lobby = PlumaLobbyRegistry.TryGetCurrent();
        if (lobby != null && lobby.NetService.Type.IsMultiplayer())
        {
            GD.Print($"[pluma] SkinSelector: 多人模式，写入大厅暂存槽 (netId={lobby.NetService.NetId}, index={index})");
            PlumaSkins.SelectSkinInLobby(lobby, lobby.NetService.NetId, index);
        }
        else
        {
            GD.Print($"[pluma] SkinSelector: 单人模式或无大厅 (lobby={(lobby != null ? lobby.NetService.Type.ToString() : "null")}), 写入本地配置 index={index}");
            PlumaSkins.SelectSkinLocal(index);
        }
        UpdateSelectedSkin();
    }

    // 把本地皮肤推送到联机大厅（仅在多人模式下生效）
    private void PushLocalSkinToLobby()
    {
        var lobby = PlumaLobbyRegistry.TryGetCurrent();
        if (lobby == null)
        {
            GD.Print("[pluma] SkinSelector: 未找到当前大厅，跳过皮肤推送");
            return;
        }
        if (!lobby.NetService.Type.IsMultiplayer())
        {
            GD.Print($"[pluma] SkinSelector: 非多人模式 ({lobby.NetService.Type})，跳过皮肤推送");
            return;
        }
        GD.Print($"[pluma] SkinSelector: 推送本地皮肤到大厅 (netId={lobby.NetService.NetId}, index={PlumaSkins.LocalIndex})");
        PlumaSkins.SelectSkinInLobby(lobby, lobby.NetService.NetId, PlumaSkins.LocalIndex);
    }

    private void UpdateSelectedSkin()
    {
        // 多人模式下优先显示大厅暂存槽中的值，否则显示本地值
        int index = PlumaSkins.CurrentIndex;
        var lobby = PlumaLobbyRegistry.TryGetCurrent();
        if (lobby != null && lobby.NetService.Type.IsMultiplayer() &&
            PlumaSkins.TryGetLobbySkinIndex(lobby, lobby.NetService.NetId, out int lobbyIndex))
        {
            index = lobbyIndex;
        }

        var skin = PlumaSkins.Skins[index];
        _skinNameLabel.Text = skin.Name;

        _leftButton.Modulate = index == 0 ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
        _rightButton.Modulate = index == 1 ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
    }
}
