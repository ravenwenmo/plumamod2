using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 黎博利：造成伤害时，给目标附加1层创伤。
[RegisterPower]
public class LiberiPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 可叠加，获得后永久生效
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/LiberiPower.png",
        BigIconPath: "res://pluma/images/powers/LiberiPower.png"
    );

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (dealer == base.Owner && props.IsPoweredAttack() && result.UnblockedDamage > 0)
		{
			await PowerCmd.Apply<OpenWoundPower>(choiceContext, target, base.Amount, base.Owner, null);
		}
	}

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };
}