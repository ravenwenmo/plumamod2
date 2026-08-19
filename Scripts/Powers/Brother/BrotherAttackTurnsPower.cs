using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 剑走偏锋：龙舌兰攻击循环意图的剩余回合数，层数即剩余回合。
[RegisterPower]
public class BrotherAttackTurnsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherAttackTurns.png",
        BigIconPath: "res://pluma/images/powers/BrotherAttackTurns.png"
    );

    // 获得能力后，切换为攻击循环意图
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await (Owner.Monster as Brother)?.SwitchToAttackIntent();
    }
}