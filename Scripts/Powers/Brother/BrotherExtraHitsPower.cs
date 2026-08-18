using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 多段斩：龙舌兰攻击循环意图的群体伤害段数增加等同于层数的次数（可叠加）。
// 段数读取见 Brother.OnSideTurnEnd。
[RegisterPower]
public class BrotherExtraHitsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherExtraHits.png",
        BigIconPath: "res://pluma/images/powers/BrotherExtraHits.png"
    );
}