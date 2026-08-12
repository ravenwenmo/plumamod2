using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Pluma.Scripts;

// 收割者：造成攻击伤害时，回复等于层数的生命值。
[RegisterPower]
public class ReaperHealingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ReaperHealingPower.png",
        BigIconPath: "res://pluma/images/powers/ReaperHealingPower.png"
    );

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props,
        Creature target, CardModel? cardSource)
    {
        // 只处理自己造成的伤害
        if (dealer != base.Owner) return;

        // 只要目标不是自己（避免自伤回血），就回血
        if (target == base.Owner) return;
        // 回复 = 层数
        await CreatureCmd.Heal(base.Owner, base.Amount);
    }
}