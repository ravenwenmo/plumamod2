using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 义兄妹の羁绊：你的回合开始时，龙舌兰强化循环意图下获得力量（达到阈值切换为攻击循环）；
// 你的回合结束时，龙舌兰攻击循环意图下造成群体伤害；
// 攻击循环意图期间，龙舌兰会吸收所有未被格挡的攻击伤害。
[RegisterPower]
public class BrotherSupportPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherSupportPower.png",
        BigIconPath: "res://pluma/images/powers/BrotherSupportPower.png"
    );

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;

        if (Owner.Player == null)
        {
            throw new Exception("BrotherSupportPower: Only players can have BrotherSupportPower.");
        }

        if (!Owner.Player.IsBrotherAlive())
        {
            await PowerCmd.Remove<BrotherSupportPower>(Owner);
        }
        else
        {
            Creature brother = Owner.Player.Brother();
            await (brother.Monster as Brother)?.TakeTurn(choiceContext);
            foreach (PowerModel power in brother.Powers.ToList())
            {
                await power.BeforeSideTurnEndEarly(choiceContext, side, [brother]);
            }
            foreach (PowerModel power in brother.Powers.ToList())
            {
                await power.AfterSideTurnEnd(choiceContext, side, [brother]);
            }
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;

        if (Owner.Player == null)
        {
            throw new Exception("BrotherSupportPower: Only players can have BrotherSupportPower.");
        }

        if (!Owner.Player.IsBrotherAlive())
        {
            await PowerCmd.Remove<BrotherSupportPower>(Owner);
        }
        else
        {
            await (Owner.Player.Brother().Monster as Brother)?.PlayerTurnStart();
        }
    }
}