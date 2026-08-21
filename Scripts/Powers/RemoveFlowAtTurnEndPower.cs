using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 回合结束时移除所有渐入佳境。
[RegisterPower]
public class RemoveFlowAtTurnEndPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single; // 不可叠加

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/RemoveFlowAtTurnEndPower.png",
        BigIconPath: "res://pluma/images/powers/RemoveFlowAtTurnEndPower.png"
    );

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner)) return;

        // 完全移除渐入佳境
        await PowerCmd.Remove<FlowState>(base.Owner);
    }
}