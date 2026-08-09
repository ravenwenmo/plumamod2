using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Combat;          // 提供 CombatSide, ICombatState


namespace Pluma.Scripts;

// 假动作（临时）：回合结束时触发多次创伤，然后自动移除。
[RegisterPower]
public class FeintPower : ModTemporaryAppliedPowerTemplate<Feint, FeintPower>
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Feint.png",
        BigIconPath: "res://pluma/images/powers/Feint.png"
    );

    protected override bool IsPositive => false; // 负面效果
    protected override bool UntilEndOfOtherSideTurn => true; // 在敌人回合结束时过期（因为施加给敌人）

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(base.Owner)) return;

        // 获取自身的创伤能力
        var wound = base.Owner.Powers.OfType<OpenWoundPower>().FirstOrDefault();
        if (wound == null || wound.Amount <= 0) return;

        int triggers = (int)base.Amount; // 假动作的层数

        for (int i = 0; i < triggers; i++)
        {
            wound = base.Owner.Powers.OfType<OpenWoundPower>().FirstOrDefault();
            if (wound == null || wound.Amount <= 0) break;

            await CreatureCmd.Damage(
                choiceContext,
                base.Owner,
                wound.Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null
            );
            await PowerCmd.Decrement(wound);
        }
    }
}