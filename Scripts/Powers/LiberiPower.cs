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

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props,
        Creature target, CardModel? cardSource)
    {
        // 只处理持有者造成的伤害，且目标存活，且不是创伤自身造成的伤害（防递归）
        if (dealer != base.Owner || target == null || !target.IsAlive || target == base.Owner) return;
        // 自残（？）不会
        if (props.HasFlag(ValueProp.Unpowered) && props.HasFlag(ValueProp.Unblockable) && cardSource == null) return;
        
        // 避免创伤伤害再次触发附加（通过检查伤害属性）
        if (props.HasFlag(ValueProp.Unpowered) && props.HasFlag(ValueProp.Unblockable)) return;

        // 给目标附加1层创伤
        await PowerCmd.Apply<OpenWoundPower>(
            choiceContext,
            target,
            base.Amount,
            base.Owner,
            cardSource
        );
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };
}