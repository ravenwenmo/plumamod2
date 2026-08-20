using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 蓄力：攻击牌打出次数+1，打出后消耗1层；玩家回合结束自动移除。
// 龙舌兰持有此能力时：回合结束不移除，而是当攻击循环即将结束（BrotherAttackTurnsPower 归零）时，
// 消耗1层蓄力并使攻击循环回合数+1，从而延长攻击循环。
[RegisterPower]
public class ChargingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ChargingPower.png",
        BigIconPath: "res://pluma/images/powers/ChargingPower.png"
    );

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner.Creature != base.Owner)
        {
            return playCount;
        }
        if (card.Type != CardType.Attack)
        {
            return playCount;
        }
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        await PowerCmd.Decrement(this);
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

        // 龙舌兰持有此能力时，不在回合结束移除，留待攻击循环结束逻辑处理
        if (Owner.Monster is Brother)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只处理龙舌兰身上的 BrotherAttackTurnsPower 层数变化
        if (power is not BrotherAttackTurnsPower || power.Owner != Owner) return;
        if (Owner.Monster is not Brother) return;

        // 当攻击循环剩余回合归零时，消耗1层蓄力并给攻击回合+1
        if (amount <= 0 && Amount > 0)
        {
            await PowerCmd.Decrement(this);
            await PowerCmd.Apply<BrotherAttackTurnsPower>(
                choiceContext,
                Owner,
                1m,
                Owner,
                null
            );
        }
    }
}