using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 灵巧（增益）：本回合内，自己打出的攻击段数翻倍、自己施加的创伤层数翻倍。
// 回合结束时自动移除。
[RegisterPower]
public class DeftnessPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    // 只持续到本回合结束，层数无意义，隐藏数字
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/DeftnessPower.png",
        BigIconPath: "res://pluma/images/powers/DeftnessPower.png"
    );

    // 攻击段数翻倍：只影响自己拥有的攻击牌（卡面预览的 HitCount 也走这个钩子）
    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        if (attack.ModelSource is not CardModel { Type: CardType.Attack } card) return hitCount;
        if (card.Owner != base.Owner.Player) return hitCount;
        return hitCount * 2;
    }

    // 施加的创伤翻倍：只影响自己通过卡牌施加给任意目标的创伤
    public override decimal ModifyPowerAmountGivenMultiplicative(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource)
    {
        if (power is not OpenWoundPower) return 1m;
        if (giver != base.Owner) return 1m;
        if (cardSource == null || cardSource.Owner != base.Owner.Player) return 1m;
        return 2m;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner))
        {
            return;
        }

        await PowerCmd.Remove(this);
    }
}
