using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 假动作：回合结束时，每层触发一次创伤效果，然后消失。
[RegisterPower]
public class FeintPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Feint.png",
        BigIconPath: "res://pluma/images/powers/Feint.png"
    );


    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        // 只在敌方回合开始时触发
        if (side != CombatSide.Enemy) return;
        if (!participants.Contains(base.Owner)) return;

        var wound = base.Owner.Powers.OfType<OpenWoundPower>().FirstOrDefault();

        // 没有创伤则直接移除自身
        if (wound == null || wound.Amount <= 0)
        {
            await PowerCmd.Remove(this);
            return;
        }

        int triggers = (int)base.Amount; // 假动作层数

        for (int i = 0; i < triggers; i++)
        {
            wound = base.Owner.Powers.OfType<OpenWoundPower>().FirstOrDefault();
            if (wound == null || wound.Amount <= 0) break;

            // 使用 ThrowingPlayerChoiceContext 代替 choiceContext
            await CreatureCmd.Damage(
                new ThrowingPlayerChoiceContext(),
                base.Owner,
                wound.Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null
            );
            await PowerCmd.Decrement(wound);
        }

        // 触发完毕后移除自身（一次性效果）
        await PowerCmd.Remove(this);
    }
}