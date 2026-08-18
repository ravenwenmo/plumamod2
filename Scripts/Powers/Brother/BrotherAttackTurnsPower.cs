using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 攻击循环（显示用）：龙舌兰攻击循环意图的剩余回合数，层数即剩余回合。
// 进入攻击循环时按 ATTACK_INTENT_TURNS 施加，每回合递减，循环结束时移除；
// 实际逻辑以 BrotherStateData.AttackTurnsRemaining 为准，本能力仅用于界面同步显示。
[RegisterPower]
public class BrotherAttackTurnsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherAttackTurns.png",
        BigIconPath: "res://pluma/images/powers/BrotherAttackTurns.png"
    );
}