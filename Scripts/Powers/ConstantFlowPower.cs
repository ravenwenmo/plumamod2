using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 源源不断：每回合开始获得等于层数的渐入佳境。
[RegisterPower]
public class ConstantFlowPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ConstantFlow.png",
        BigIconPath: "res://pluma/images/powers/ConstantFlow.png"
    );

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(base.Owner) && base.Amount > 0)
        {
            await PowerCmd.Apply<FlowState>(
                new ThrowingPlayerChoiceContext(),
                base.Owner,
                base.Amount,          // 每层获得1层渐入佳境
                base.Owner,
                null
            );
        }
    }
}