using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts.CombatSolverAdapter;

/// <summary>
/// 始终编译的桥状态。BattleRecorder 等不引用 CombatSolver 程序集的代码从这里读取适配状态。
/// </summary>
public static class CombatSolverBridgeStatus
{
    /// <summary>CombatSolver 适配是否已激活（门禁已放行 + 镜像已登记）。</summary>
    public static bool IsActive { get; internal set; }

    /// <summary>运行期探测到的 CombatSolver 版本字符串，未加载时为 null。</summary>
    public static string? LoadedVersion { get; internal set; }
}

#if PLUMA_COMBATSOLVER_ADAPTER
/// <summary>
/// pluma ↔ CombatSolver 软适配入口（战斗内适配按 CombatSolver 的 THIRD_PARTY_ADAPTERS.md）。
///
/// 设计要点：
/// 1. 软依赖：CombatSolver 程序集缺失时本类完全不工作，pluma 就是普通人物 Mod；
/// 2. 版本钉死 + 运行期自检：仅支持 [0.35.4, 0.36.0)，自检失败一个镜像都不登记，
///    让 CombatSolver 的门禁明确拒绝 pluma，而不是带半套镜像给出错误路线（手册 §3.2/§3.4）；
/// 3. 门禁放行：把 pluma 的战斗 hook 订阅器类型写进 CombatSolver 的私有白名单
///    KnownPreRootSubscriberTypeNames（手册 §1.1 目前唯一入口）；
/// 4. 所有日志带 [鸡煲] 前缀。
/// </summary>
public static class CombatSolverBridge
{
    private const string AssemblyName = "CombatSolver";

    // 钉死当前游戏实际加载的工坊版（Steam 会优先加载版本更高的一份）。
    // 每次 CombatSolver 升小版本后，先升级本地引用重编译、核对自检，再放开这里。
    private static readonly Version MinSupported = new(0, 36, 0);
    private static readonly Version MaxExclusive = new(0, 37, 0);

    private static bool _started;
    private static bool _disabled;

    /// <summary>在 Entry.Init 里调用一次。之后通过 AssemblyLoad 事件兜底探测晚加载。</summary>
    public static void Initialize()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        TryActivate(logWhenMissing: true);
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
    }

    private static void OnAssemblyLoaded(object? sender, AssemblyLoadEventArgs args)
    {
        if (args.LoadedAssembly.GetName().Name == AssemblyName)
        {
            TryActivate(logWhenMissing: false);
        }
    }

    /// <summary>枚举 pluma 程序集里所有 AbstractModel 子类型（战斗订阅器候选）。</summary>
    private static Type[] DiscoverPlumaSubscriberTypes()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(AbstractModel).IsAssignableFrom(t))
            .ToArray();
    }

    private static void TryActivate(bool logWhenMissing)
    {
        if (_disabled || CombatSolverBridgeStatus.IsActive)
        {
            return;
        }

        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == AssemblyName);
        if (assembly == null)
        {
            if (logWhenMissing)
            {
                Entry.Logger.Info("[鸡煲] 未检测到 CombatSolver，适配层保持关闭（pluma 以普通手动战斗运行）");
            }
            return;
        }

        Version version = assembly.GetName().Version ?? new Version(0, 0, 0, 0);
        CombatSolverBridgeStatus.LoadedVersion = version.ToString(3);
        if (version < MinSupported || version >= MaxExclusive)
        {
            _disabled = true;
            Entry.Logger.Warn(
                $"[鸡煲] CombatSolver 版本 {version} 不在支持范围 [{MinSupported}, {MaxExclusive})，" +
                "适配层停用，回退手动战斗");
            return;
        }

        // 运行期自检第一步：门禁白名单字段必须存在，否则不登记任何镜像。
        FieldInfo? whitelistField = assembly
            .GetType("CombatSolver.PredictionModHookSubscriberCapture", throwOnError: false)
            ?.GetField("KnownPreRootSubscriberTypeNames", BindingFlags.NonPublic | BindingFlags.Static);

        try
        {
            CombatSolverMirrors.RegisterAll();

            if (whitelistField?.GetValue(null) is not HashSet<string> whitelist)
            {
                throw new InvalidOperationException("CombatSolver 的订阅器白名单字段缺失，无法放行 pluma");
            }

            // 动态枚举 pluma 全部战斗订阅器类型（卡片/Power/遗物/单例），
            // 避免漏掉任何一个非惰性 hook 覆写触发门禁。未被镜像的覆写在搜索里会
            // 自动记风险红字（CombatSolver 自身行为），不会静默通过。
            Type[] subscribers = DiscoverPlumaSubscriberTypes();
            foreach (Type subscriber in subscribers)
            {
                whitelist.Add(subscriber.FullName ?? subscriber.Name);
            }

            CombatSolverBridgeStatus.IsActive = true;
            Entry.Logger.Info(
                $"[鸡煲] 已接管战斗适配：CombatSolver {version} 已放行 {subscribers.Length} 个 pluma 订阅器，" +
                "核心机制镜像登记完成");
        }
        catch (Exception ex)
        {
            // 自检失败：一个镜像都不留。之后门禁会把 pluma 判为不兼容并停摆，
            // 玩家能明确看到原因，而不是拿到看似可信的错误路线。
            _disabled = true;
            CombatSolverBridgeStatus.IsActive = false;
            Entry.Logger.Error($"[鸡煲] CombatSolver 适配自检失败，已放弃全部登记并回退手动战斗: {ex}");
        }
    }
}
#endif
