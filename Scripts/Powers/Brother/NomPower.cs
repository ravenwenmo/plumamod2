using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts.Monsters; // 如果需要引用 BrotherStateData
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 嚼：龙舌兰下一次攻击斩杀敌人时，提升3点最大生命值，然后消耗1层。可叠加。
[RegisterPower]
public class NomPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/NomPower.png",
        BigIconPath: "res://pluma/images/powers/NomPower.png"
    );

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        // 只处理龙舌兰发起的攻击
        if (command.Attacker != Owner) return;
        if (Amount <= 0) return;

        bool killedAny = command.Results
            .SelectMany(r => r)
            .Any(r => r.WasTargetKilled);

        if (!killedAny) return;

        // 提升最大生命值3点
        int newMax = Owner.MaxHp + 3;
        await CreatureCmd.SetMaxHp(Owner, newMax);

        // 同步持久化数据
        if (Owner.PetOwner != null)
        {
            BrotherStateData.SetHp(Owner.PetOwner, Owner.CurrentHp, newMax);
            // 或者 BrotherStateData.SetFromBrother(Owner.PetOwner, Owner);
        }

        // 消耗一层能力
        await PowerCmd.Decrement(this);
    }
}