using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace Pluma.Scripts;

// 基酒手牌上限：玩家手牌中每有一张基酒牌，手牌上限 +1
[RegisterSingleton]
public class BaseSpiritHandSizeSingleton : HookedSingletonModel, IMaxHandSizeModifier
{
    public BaseSpiritHandSizeSingleton() : base(HookType.Combat)
    {
    }

    // 早期修正：按手牌中基酒牌数量增加上限
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
    {
        if (player == null) return currentMaxHandSize;

        // 多人模式身份校验：只响应当前战斗中的玩家
        if (CurrentCombatState == null || !CurrentCombatState.Players.Contains(player))
            return currentMaxHandSize;

        var handPile = PileType.Hand.GetPile(player);
        int baseSpiritCount = handPile.Cards.Count(c => c is IBaseSpiritCard);
        return currentMaxHandSize + baseSpiritCount;
    }

    // 后期修正：基酒加成无需再叠加，原样返回
    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) => currentMaxHandSize;
}
