using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

[RegisterPower]
public class RapidSlashingStacks : ModPowerTemplate
{
    // 类型，Buff或Debuff
    public override PowerType Type => PowerType.Buff;
    // 叠加类型，Counter表示可叠加，Single表示不可叠加
    public override PowerStackType StackType => PowerStackType.Counter;
    // 实例类型，默认会在已有的能力上堆叠。如果是Instanced，则每次都会新建一个实例。（像炸弹那样）
    // public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    // 自定义图标路径。1:1即可。原版游戏大图256x256，小图64x64。
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/RapidSlashingStacks.png",
        BigIconPath: "res://pluma/images/powers/RapidSlashingStacks.png"
    );

    // ===== run 内跨战斗持久化：层数变化/移除时同步到 run 数据槽 =====

    // 层数变化（首次施加、叠层、削减）后把当前层数写入 run 数据槽。
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            RapidSlashingStacksPersistence.Sync(this);
        }
        return Task.CompletedTask;
    }

    // 能力被正常移除（如达到阈值被消耗）时把槽位层数清零。
    // 注意：战斗结束时的静默清能力（RemoveInternal）不会走到这里，槽位保留最后层数。
    public override Task AfterRemoved(Creature oldOwner)
    {
        RapidSlashingStacksPersistence.Clear(oldOwner);
        return Task.CompletedTask;
    }
}
