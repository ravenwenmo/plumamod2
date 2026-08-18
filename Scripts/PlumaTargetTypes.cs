using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2RitsuLib;

namespace Pluma.Scripts;

/// <summary>
/// 模组自定义目标类型。
///
/// RitsuLib 提供完整的自定义目标管线（`RitsuLibFramework.RegisterSingleTargetType`）：
/// 拖拽箭头、手柄选目标、自动打出随机目标、CanPlayTargeting / IsValidTarget 检查等都会
/// 自动识别注册的自定义类型。注册值是确定性的（由 modId + 词干派生），多人各端一致。
/// </summary>
public static class PlumaTargetTypes
{
    private static TargetType? _anyUnit;

    /// <summary>
    /// "任意单位"：允许选择任意存活单位（敌人、自己、友方玩家、宠物如龙舌兰）。
    /// 宠物（如龙舌兰）位于玩家侧，SpiritTargeting 会将其解析为 Ally 分支，
    /// 享受与友方单位相同的效果。
    /// 卡牌打出后根据所选目标的阵营执行不同效果。
    /// </summary>
    public static TargetType AnyUnit => _anyUnit ??= RitsuLibFramework.RegisterSingleTargetType(
        Entry.ModId, "any_unit",
        creature => creature != null && creature.IsAlive);
}
