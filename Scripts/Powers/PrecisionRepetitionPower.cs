using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 精密重复：持有此能力时，切割连击计数不会因打出非切割牌而中断。
[RegisterPower]
public class PrecisionRepetitionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/PrecisionRepetition.png",
        BigIconPath: "res://pluma/images/powers/PrecisionRepetition.png"
    );
}