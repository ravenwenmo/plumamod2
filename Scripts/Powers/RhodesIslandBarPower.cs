using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 罗德岛酒吧：玩家手牌中每有一张鸡尾酒牌，手牌上限额外 +1（与基酒加成叠加）
[RegisterPower]
public class RhodesIslandBarPower : ModPowerTemplate, IMaxHandSizeModifier
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/RhodesIslandBarPower.png",
        BigIconPath: "res://pluma/images/powers/RhodesIslandBarPower.png"
    );

    // 早期修正：只对能力持有者生效，按手牌中鸡尾酒牌数量增加上限
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
    {
        if (player == null || player != base.Owner.Player) return currentMaxHandSize;

        var handPile = PileType.Hand.GetPile(player);
        int cocktailCount = handPile.Cards.Count(c => c is ICocktailCard);
        return currentMaxHandSize + cocktailCount;
    }

    // 后期修正：鸡尾酒加成无需再叠加，原样返回
    public int ModifyMaxHandSizeLate(Player player, int currentMaxHandSize) => currentMaxHandSize;
}
