using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.RunData;

namespace Pluma.Scripts;

public static class PlumaSkins
{
    // 本地持久化（单人模式 / 默认值）
    private const string ConfigPath = "user://pluma_skin.cfg";
    private const string Section = "Skin";
    private const string Key = "Index";

    // 多人同步槽位（从 Entry 获取）
    private static PlayerRunSavedData<SkinIndexWrapper> Slot => Entry.SkinData;

    public static readonly IReadOnlyList<SkinData> Skins = new List<SkinData>
    {
        new()
        {
            Name = "默认",
            VisualsPath = "res://pluma/images/spineAni/ori/ori_character.tscn",
            CharacterSelectBgPath = "res://pluma/images/scenes/ori_background_skins.tscn",
            CharacterSelectBgPathMulti = "res://pluma/images/scenes/ori_background_skins_mul.tscn", // 多人模式
            IconPath = "res://pluma/images/spineAni/ori/icon/test_icon.tscn",
            IconTexturePath = "res:///pluma/images/spineAni/ori/icon/head.png",
            PortraitPath = "res://pluma/images/scenes/test_background.png",
            BackgroundColor = new Color(0.209f, 0.623f, 0.734f)
        },
        new()
        {
            Name = "夏卉",
            VisualsPath = "res://pluma/images/spineAni/sum/sum_character.tscn",
            CharacterSelectBgPath = "res://pluma/images/scenes/ori_background_skins.tscn",
            CharacterSelectBgPathMulti = "res://pluma/images/scenes/ori_background_skins_mul.tscn",
            IconPath = "res://pluma/images/spineAni/ori/icon/test_icon.tscn",
            IconTexturePath = "res:///pluma/images/spineAni/ori/icon/head.png",
            PortraitPath = "res://pluma/images/scenes/test_background.png",
            BackgroundColor = new Color(0.5f, 0.2f, 0.8f)
        }
    };

    // 本地索引
    public static int LocalIndex
    {
        get
        {
            var config = new ConfigFile();
            if (config.Load(ConfigPath) == Error.Ok)
                return (int)config.GetValue(Section, Key, 0).AsInt32();
            return 0;
        }
        private set
        {
            var config = new ConfigFile();
            config.SetValue(Section, Key, value);
            config.Save(ConfigPath);
        }
    }

    // 获取玩家皮肤索引（局内从 RunState，否则回退本地）
    public static int GetSkinIndex(Player? player = null)
    {
        if (player != null && Slot != null)
        {
            var runState = player.RunState as RunState;
            if (runState != null)
            {
                var wrapper = Slot.Get(runState, player.NetId);
                return Mathf.Clamp(wrapper.Index, 0, Skins.Count - 1);
            }
        }
        return Mathf.Clamp(LocalIndex, 0, Skins.Count - 1);
    }

    // 获取皮肤数据
    public static SkinData GetCurrentSkin(Player? player = null) => Skins[GetSkinIndex(player)];

    // 单人模式切换
    public static void SelectSkinLocal(int skinIndex)
    {
        skinIndex = Mathf.Clamp(skinIndex, 0, Skins.Count - 1);
        LocalIndex = skinIndex;
    }

    // 多人模式大厅切换（自动同步）
    public static void SelectSkinInLobby(StartRunLobby lobby, ulong playerNetId, int skinIndex)
    {
        skinIndex = Mathf.Clamp(skinIndex, 0, Skins.Count - 1);
        Slot?.Lobby.Modify(lobby, playerNetId, wrapper => wrapper.Index = skinIndex);
    }

    // 旧接口兼容
    public static int CurrentIndex { get => LocalIndex; set => SelectSkinLocal(value); }
    public static SkinData Current => Skins[CurrentIndex];
    public static void Next() => SelectSkinLocal((CurrentIndex + 1) % Skins.Count);
    public static void Previous() => SelectSkinLocal((CurrentIndex - 1 + Skins.Count) % Skins.Count);
    public static void SetIndex(int index) => SelectSkinLocal(index);

    public class SkinData
    {
        public string Name { get; init; }
        public string VisualsPath { get; init; }
        public string CharacterSelectBgPath { get; init; }
        public string CharacterSelectBgPathMulti { get; init; } // 多人模式背景
        public string IconPath { get; init; }
        public string IconTexturePath { get; init; }
        public string PortraitPath { get; init; }
        public Color BackgroundColor { get; init; }
    }
}