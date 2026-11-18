using System;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 轻盈：手牌上限 -层数（每层 -1）。本场战斗内永久、可叠加，最低降到 0。
// 手牌上限的改法照「基酒」那套来：实现 RitsuLib 的 IMaxHandSizeModifier，
// RitsuLib 会把游戏里所有 CardPile.MaxCardsInHand 的读取点（抽牌上限、手牌满判定等）改写成这里的结果。
// 注意：上限降低时本体不会把超出的手牌弃掉，只是不能再抽（玩家决策 m01285 确认的口径）。
[RegisterPower]
public class FeatherlightPower : ModPowerTemplate, IMaxHandSizeModifier
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/FeatherlightPower.png",
        BigIconPath: "res://pluma/images/powers/FeatherlightPower.png"
    );

    // 早期修正：只对能力持有者生效，按层数减少手牌上限
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
    {
        if (player == null || player != base.Owner.Player) return currentMaxHandSize;

        // 下限 0：层数叠得再多也只是不能再抽牌
        return Math.Max(0, currentMaxHandSize - (int)base.Amount);
    }

    // 后期修正：无需再叠加，原样返回
    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) => currentMaxHandSize;
}
