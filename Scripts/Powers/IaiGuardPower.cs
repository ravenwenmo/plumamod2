using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Pluma.Scripts;

// 居合减伤：由居合自动打出时施加50层。每层提供1%伤害减免（对来自敌方怪物、目标是玩家自身的伤害）。
// 伤害减免作用于最终扣血量，而不是攻击伤害数值。伤害事件结束后自动移除，无论是否实际扣血。
[RegisterPower]
public class IaiGuardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/Trait.png",
        BigIconPath: "res://pluma/images/powers/Trait.png"
    );

    // 改为最终生命损失修正，保证减伤实际生效
    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && target is { IsPlayer: true } && dealer is { IsMonster: true })
        {
            // 每层减伤1%，50层 = 50%
            return amount * (1m - (decimal)Amount / 100m);
        }
        return amount;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 本次伤害事件结束，移除自身，保证一次性减伤
        if (target == Owner && dealer is { IsMonster: true })
        {
            await PowerCmd.Remove(this);
        }
    }
}