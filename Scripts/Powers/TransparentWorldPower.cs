using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 通透世界：渐入佳境抽到的牌自动打出。
[RegisterPower]
public class TransparentWorldPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TransparentWorldPower.png",
        BigIconPath: "res://pluma/images/powers/TransparentWorldPower.png"
    );
}