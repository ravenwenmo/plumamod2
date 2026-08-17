using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 优良机动：每拥有一层此能力，每当获得渐入佳境时，获得对应层数的格挡。
[RegisterPower]
public class ExcellentMobilityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ExcellentMobility.png",
        BigIconPath: "res://pluma/images/powers/ExcellentMobility.png"
    );
}