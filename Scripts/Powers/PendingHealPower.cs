using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content; 
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Pluma.Scripts;

// 待回复：显示本场战斗中已累积的龙舌兰待回复生命值。
[RegisterPower]
public class PendingHealPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/PendingHeal.png",
        BigIconPath: "res://pluma/images/powers/PendingHeal.png"
    );
}