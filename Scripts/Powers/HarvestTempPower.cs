using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards;


namespace Pluma.Scripts;

// 收割临时加成：当来源是 Harvest 且目标生命值低于一半时，伤害提高（层数）%。层数由卡牌设置（40 或 50）。
[RegisterPower]
public class HarvestTempPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 隐藏图标，只在后台生效
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/HarvestTempPower.png",
        BigIconPath: "res://pluma/images/powers/HarvestTempPower.png"
    );

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource is Harvest && target != null && target.CurrentHp < target.MaxHp / 2m)
        {
            return 1m + (decimal)Amount / 100m;
        }
        return 1m;
    }
}