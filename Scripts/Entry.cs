using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.RunData;

namespace Pluma.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "pluma";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 皮肤数据句柄（现在使用引用类型 SkinIndexWrapper）
    public static PlayerRunSavedData<SkinIndexWrapper> SkinData = null!;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        PlumaSettingsPage.Register();

        // 注册先古升级和遗物升级
        RitsuLibFramework.RegisterArchaicToothTranscendenceMapping<RapidSlashing, RapidSlashingMax>();
        RitsuLibFramework.RegisterTouchOfOrobasRefinementMapping<ReaperBadge, AquaDawn>();

        // 注册皮肤数据槽位
        using (RitsuLibFramework.BeginModDataRegistration(ModId))
        {
            var store = RitsuLibFramework.GetRunSavedDataStore(ModId);
            SkinData = store.RegisterPerPlayer(
                key: "skin_index",
                defaultFactory: () => new SkinIndexWrapper { Index = PlumaSkins.LocalIndex },
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true
                });
        }
    }
}

/// <summary>
/// 包装皮肤索引的引用类型（满足 RegisterPerPlayer 的 class 约束）
/// </summary>
public class SkinIndexWrapper
{
    public int Index { get; set; }
}