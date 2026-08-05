using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace Pluma.Scripts;

public sealed class PlumaSettings
{
    public bool Enabled { get; set; } = true;
    public int Volume { get; set; } = 80;
    public string Layout { get; set; } = "compact";
}

public static class PlumaSettingsPage
{
    private const string DataKey = "settings";

    // 设置绑定，可调用来查询值和改动
    private static readonly ModSettingsValueBinding<PlumaSettings, bool> EnabledBinding = new(
        Entry.ModId, DataKey, SaveScope.Profile,
        static s => s.Enabled,
        static (s, v) => s.Enabled = v);

    private static readonly ModSettingsValueBinding<PlumaSettings, int> VolumeBinding = new(
        Entry.ModId, DataKey, SaveScope.Profile,
        static s => s.Volume,
        static (s, v) => s.Volume = v);

    public static void Register()
    {
        // 注册 DataStore
        ModDataStore.For(Entry.ModId).Register<PlumaSettings>(
            key: DataKey, // 持久化数据ID，需要和别人防撞
            fileName: "pluma_settings.json", // 你的数据文件名
            scope: SaveScope.Profile, // Profile 表示每个存档独立，可改成 Global 表示所有存档共享
            defaultFactory: () => new PlumaSettings(),
            autoCreateIfMissing: true);

        // 注册页面UI
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(ModSettingsText.Literal("羽毛笔"))
            .WithModDisplayName(ModSettingsText.Literal("羽毛笔Mod"))
            .WithVisibleOnHostSurfaces(
                ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("general", section => section
                .WithTitle(ModSettingsText.Literal("通用"))
                .AddToggle("enabled", ModSettingsText.Literal("启用"), EnabledBinding)
                .AddIntSlider("volume", ModSettingsText.Literal("音量"), VolumeBinding,
                    minValue: 0, maxValue: 100, step: 5,
                    valueFormatter: static v => $"{v}%")
                .AddButton("reset", ModSettingsText.Literal("音量"),
                    ModSettingsText.Literal("重置"),
                    host =>
                    {
                        VolumeBinding.Write(80);
                        host.MarkDirty(VolumeBinding);
                        host.RequestRefresh();
                    },
                    ModSettingsButtonTone.Accent)
                .AddChoice("layout", ModSettingsText.Literal("布局"),
                    new ModSettingsValueBinding<PlumaSettings, string>(
                        Entry.ModId, DataKey, SaveScope.Profile,
                        static s => s.Layout,
                        static (s, v) => s.Layout = v),
                    [
                        new("compact", ModSettingsText.Literal("紧凑")),
                        new("comfortable", ModSettingsText.Literal("舒展"))
                    ],
                    presentation: ModSettingsChoicePresentation.Dropdown)));
    }
}