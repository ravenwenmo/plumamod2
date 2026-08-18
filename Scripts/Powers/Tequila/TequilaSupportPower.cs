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

// 义兄妹の羁绊：你的回合结束时，龙舌兰将采取行动；你的回合开始时，龙舌兰会切换意图；龙舌兰意图为攻击时，会吸收所有未被格挡的攻击伤害。
[RegisterPower]
public class TequilaSupportPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TequilaSupport.png",
        BigIconPath: "res://pluma/images/powers/TequilaSupport.png"
    );

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;

        if (Owner.Player == null)
        {
            throw new Exception("TequilaSupportPower: Only players can have TequilaSupportPower.");
        }

        if (!Owner.Player.IsTequilaAlive())
        {
            await PowerCmd.Remove<TequilaSupportPower>(Owner);
        }
        else
        {
            Creature tequila = Owner.Player.Tequila();
            await (tequila.Monster as Monsters.Tequila)?.Move();
            foreach (PowerModel power in tequila.Powers.ToList())
            {
                GD.Print($"[TequilaSupportPower] BeforeSideTurnEnd: Calling BeforeSideTurnEnd for power {power.GetType().Name}");
                await power.BeforeSideTurnEndEarly(choiceContext, side, [tequila]);
            }
        }
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner)) return;

        if (Owner.Player == null)
        {
            throw new Exception("TequilaSupportPower: Only players can have TequilaSupportPower.");
        }

        if (!Owner.Player.IsTequilaAlive())
        {
            await PowerCmd.Remove<TequilaSupportPower>(Owner);
        }
        else
        {
            await (Owner.Player.Tequila().Monster as Monsters.Tequila)?.SwitchIntent();
        }
    }
}