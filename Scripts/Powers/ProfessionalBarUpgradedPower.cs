using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 专业吧台（升级版）：场上每有单位一次性被施加 2 层以上[创伤]时，将一张升级过的 辅料组合包 加入手牌。
[RegisterPower]
public class ProfessionalBarUpgradedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ProfessionalBarUpgradedPower.png",
        BigIconPath: "res://pluma/images/powers/ProfessionalBarUpgradedPower.png"
    );

    // 判定条件与 ProfessionalBarPower 保持一致，只有给的小包是升级过的
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);

        if (power is not OpenWoundPower) return;
        if (amount < 2) return;   // 只有"多层"（≥2 层）才触发

        await ProfessionalBarPower.GiveMixerPack(choiceContext, base.Owner, upgraded: true);
    }
}
