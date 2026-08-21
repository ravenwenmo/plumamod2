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

// 预感：下个玩家回合开始时，获得等同于能力层数的渐入佳境，然后消失。
[RegisterPower]
public class PremonitionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 层数即获得量

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/PremonitionPower.png",
        BigIconPath: "res://pluma/images/powers/PremonitionPower.png"
    );

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // 只在自己回合开始时触发（参与者包含自己）
        if (!participants.Contains(base.Owner)) return;

        // 获得对应层数的渐入佳境
        await PowerCmd.Apply<FlowState>(
            new ThrowingPlayerChoiceContext(),
            base.Owner,
            base.Amount,
            base.Owner,
            null
        );

        // 一次性效果，触发后移除
        await PowerCmd.Remove(this);
    }
}