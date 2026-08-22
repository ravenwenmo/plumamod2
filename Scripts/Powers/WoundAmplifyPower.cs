using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 创伤翻倍标记：持有者下一次触发创伤伤害时，创伤伤害 ×(1+层数)（1层=2倍，2层=3倍）。
// 由居合命中后施加（1层，升级后2层），创伤伤害造成后由 OpenWoundPower 清空，确保只有一次增伤。
// Buff 类型：不会被人工制品阻挡。
[RegisterPower]
public class WoundAmplifyPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 隐藏图标，只在后台生效
    protected override bool IsVisibleInternal => false;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/WoundAmplifyPower.png",
        BigIconPath: "res://pluma/images/powers/WoundAmplifyPower.png"
    );

    // 兜底清理：持有者回合结束时若标记仍未被创伤消耗，自动移除
    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            return PowerCmd.Remove(this);
        }
        return Task.CompletedTask;
    }
}
