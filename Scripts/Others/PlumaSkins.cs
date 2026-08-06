using System.Collections.Generic;
using Godot;

namespace Pluma.Scripts;

public static class PlumaSkins
{
    // 配置文件路径（保存在用户数据目录）
    private const string ConfigPath = "user://pluma_skin.cfg";
    private const string Section = "Skin";
    private const string Key = "Index";

    /// <summary>
    /// 皮肤列表，每套皮肤包含需要替换的路径。
    /// </summary>
  
    public static readonly IReadOnlyList<SkinData> Skins = new List<SkinData>
    {
        new()
        {
            Name = "原皮",
            VisualsPath = "res://pluma/images/spineAni/ori/ori_character.tscn",
            CharacterSelectBgPath = "res://pluma/images/scenes/ori_background_skins.tscn",
            IconPath = "res://pluma/images/spineAni/ori/icon/test_icon.tscn",
            IconTexturePath = "res://pluma/images/1img/char_late/character2.png",
            PortraitPath = "res://pluma/images/scenes/test_background.png", // 立绘图片
            BackgroundColor = new Color(0.209f, 0.623f, 0.734f)
        },
        new()
        {
            Name = "夏卉",
            VisualsPath = "res://pluma/images/spineAni/sum/sum_character.tscn", // 你的夏卉模型路径
            CharacterSelectBgPath = "res://pluma/images/scenes/ori_background_skins.tscn",
            IconPath = "res://pluma/images/spineAni/ori/icon/test_icon.tscn",
            IconTexturePath = "res://pluma/images/1img/char_late/character2.png", // 可替换
            PortraitPath = "res://pluma/images/scenes/test_background.png", // 夏卉立绘（暂时用同一张，可替换）
            BackgroundColor = new Color(0.5f, 0.2f, 0.8f)
        }
    };
    /// <summary>
    /// 当前皮肤索引（自动读写配置文件）
    /// </summary>
    public static int CurrentIndex
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

    /// <summary>
    /// 获取当前皮肤数据
    /// </summary>
    public static SkinData Current => Skins[CurrentIndex];

    /// <summary>
    /// 切换到下一套皮肤
    /// </summary>
    public static void Next()
    {
        CurrentIndex = (CurrentIndex + 1) % Skins.Count;
    }

    /// <summary>
    /// 切换到上一套皮肤
    /// </summary>
    public static void Previous()
    {
        CurrentIndex = (CurrentIndex - 1 + Skins.Count) % Skins.Count;
    }

    /// <summary>
    /// 皮肤数据类，包含所有可能随皮肤变化的路径。
    /// </summary>
    public class SkinData
    {
        public string Name { get; init; }
        public string VisualsPath { get; init; }           // 人物模型场景
        public string CharacterSelectBgPath { get; init; } // 角色选择背景场景
        public string IconPath { get; init; }               // 头像图标场景
        public string IconTexturePath { get; init; }        // 头像图片
        public string PortraitPath { get; init; }           // 立绘图片
        public Color BackgroundColor { get; init; }         // 背景颜色（可选）
    }
    // 在 PlumaSkins 类中添加以下方法
    public static void SetIndex(int index)
    {
        if (index < 0 || index >= Skins.Count) return;
        CurrentIndex = index;
    }
    
}