using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 灵巧（减益）：本回合失去力量与敏捷，回合结束时返还。
// 结构照抄原版 TemporaryStrengthPower（原版是"临时力量"debuff，这里同时处理力量与敏捷）。
[RegisterPower]
public class DeftnessDownPower : ModPowerTemplate
{
    // 负向：失去力量/敏捷
    private const int Sign = -1;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/DeftnessDownPower.png",
        BigIconPath: "res://pluma/images/powers/DeftnessDownPower.png"
    );

    // 施加时立刻扣除力量与敏捷（silent 避免重复播特效）
    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(), target, Sign * amount, applier, cardSource, silent: true);
        await PowerCmd.Apply<DexterityPower>(
            new ThrowingPlayerChoiceContext(), target, Sign * amount, applier, cardSource, silent: true);
    }

    // 层数变化时同步扣除（例如同一回合内连续打出两张灵巧）
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount == base.Amount) return;

        await PowerCmd.Apply<StrengthPower>(
            choiceContext, base.Owner, Sign * amount, applier, cardSource, silent: true);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext, base.Owner, Sign * amount, applier, cardSource, silent: true);
    }

    // 回合结束时移除自身并返还力量与敏捷
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner)) return;

        // 先记录层数：移除自身后 Amount 可能被清零
        decimal amount = base.Amount;
        Flash();
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<StrengthPower>(
            choiceContext, base.Owner, -Sign * amount, base.Owner, null);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext, base.Owner, -Sign * amount, base.Owner, null);
    }
}
