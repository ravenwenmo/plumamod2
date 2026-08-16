using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Pluma.Scripts;

// 卡牌选目标时的纯客户端视觉状态：记录本机当前正在瞄准的牌与指向的目标。
// 供血条预测（OpenWoundPower）与卡牌描述切换（SpiritModeDescriptionPatch）读取，
// 不写入任何游戏状态，多人模式下不需要同步。
internal static class CardAimPreview
{
    public static CardModel? CurrentCard { get; private set; }
    public static Creature? CurrentTarget { get; private set; }

    /// <summary>是否正在用 <typeparamref name="TCard"/> 瞄准指定目标。</summary>
    public static bool IsAimingWith<TCard>(Creature? target)
        where TCard : CardModel
        => CurrentCard is TCard && ReferenceEquals(CurrentTarget, target);

    /// <summary>
    /// 若 card 正在被瞄准，返回瞄准目标对应的阵营分支（自己/敌人/友方）；
    /// 否则返回 null。
    /// </summary>
    public static SpiritTargetBranch? GetAimBranchFor(CardModel card)
    {
        if (!ReferenceEquals(CurrentCard, card) || CurrentTarget == null || card.Owner == null)
            return null;
        return SpiritTargeting.Resolve(CurrentTarget, card.Owner.Creature);
    }

    public static void SetAim(CardModel? card, Creature? target)
    {
        if (ReferenceEquals(CurrentCard, card) && ReferenceEquals(CurrentTarget, target)) return;

        Creature? previousTarget = CurrentTarget;
        CurrentCard = card;
        CurrentTarget = target;

        // 瞄准变化时立即刷新受影响敌人的血条预测（兜割的创伤预测）
        RefreshHealthBar(previousTarget);
        RefreshHealthBar(target);
    }

    private static void RefreshHealthBar(Creature? creature)
    {
        if (creature == null || NCombatRoom.Instance?.GetCreatureNode(creature) is not { } creatureNode)
            return;
        // "%HealthBar" 唯一名在 NCreature 场景下先解析到 NCreatureStateDisplay，
        // 在其场景内再解析到真正的 NHealthBar。
        if (creatureNode.GetNodeOrNull("%HealthBar") is not NCreatureStateDisplay stateDisplay)
            return;
        if (stateDisplay.GetNodeOrNull("%HealthBar") is NHealthBar healthBar)
            healthBar.RefreshValues();
    }
}
