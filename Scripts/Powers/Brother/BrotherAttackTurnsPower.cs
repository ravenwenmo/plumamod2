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

// 当机立断：龙舌兰攻击循环意图的剩余回合数，层数即剩余回合。
[RegisterPower]
public class BrotherAttackTurnsPower : ModPowerTemplate
{
    // 从能力列表 UI 隐藏：本能力只是攻击循环剩余回合的计数器，
    // 对外展示整合进 TraitPower（特性）的描述中。隐藏不影响任何逻辑（官方机制，见 PowerModel.IsVisibleInternal）。
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherAttackTurnsPower.png",
        BigIconPath: "res://pluma/images/powers/BrotherAttackTurnsPower.png"
    );

    // 获得能力后，切换为攻击循环意图
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        await (Owner.Monster as Brother)?.SwitchToAttackIntent();
    }
}