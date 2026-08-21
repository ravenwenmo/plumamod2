using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;



using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 临时额外攻击段数：用于标记本次临时获得的额外攻击段数层数。
// 当龙舌兰完成一次攻击后，移除与自身层数相同层数的永久额外攻击段数（BrotherExtraHitsPower），
// 然后移除自身，从而达成“临时获得额外攻击段数一回合”的效果。
// 若龙舌兰获得本能力后本回合未攻击，则在回合结束时兜底清理，避免残留。
[RegisterPower]
public class TemporaryExtraHitsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TemporaryExtraHitsPower.png",
        BigIconPath: "res://pluma/images/powers/TemporaryExtraHitsPower.png"
    );

    /// <summary>
    /// 龙舌兰攻击完成后调用：扣除对应层数的永久额外攻击段数并移除自身。
    /// </summary>
    public async Task ConsumeAfterAttack(PlayerChoiceContext choiceContext)
    {
        int tempAmount = (int)Amount;
        if (tempAmount > 0)
        {
            int currentExtraHits = Owner.GetPowerAmount<BrotherExtraHitsPower>();
            int removeAmount = Math.Min(tempAmount, currentExtraHits);

            if (removeAmount > 0)
            {
                await PowerCmd.Apply<BrotherExtraHitsPower>(
                    choiceContext,
                    Owner,
                    -(decimal)removeAmount,
                    Owner,
                    null
                );
            }
        }

        await PowerCmd.Remove(this);
    }
}