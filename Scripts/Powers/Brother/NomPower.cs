using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
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
        IconPath: "res://pluma/images/powers/Nom.png",
        BigIconPath: "res://pluma/images/powers/Nom.png"
    );

    public override async Task AfterAttack(
        PlayerChoiceContext choiceContext,
        AttackCommand command)
    {
        // 只处理龙舌兰发起的攻击
        if (command.Attacker != Owner) return;
        if (Amount <= 0) return;

        // 检查这次攻击是否至少斩杀了一个敌人
        bool killedAny = command.Results
            .SelectMany(r => r)
            .Any(r => r.WasTargetKilled);

        if (!killedAny) return;

        // 提升最大生命值3点
        await CreatureCmd.GainMaxHp(Owner, 3);

        // 消耗一层能力
        await PowerCmd.Decrement(this);
    }
}