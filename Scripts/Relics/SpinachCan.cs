using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 绿叶菜罐头：获得渐入佳境时，额外获得1层活力。
[RegisterRelic(typeof(PlumaRelicPool))]
public class SpinachCan : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    // 用于记录上一次渐入佳境层数，判断是否是“增加”
    private int _lastFlowAmount = -1;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        // 进入战斗房间时，初始化或更新基准层数，避免上一场残留值误判
        if (room is CombatRoom && Owner?.Creature != null)
        {
            _lastFlowAmount = Owner.Creature.GetPowerAmount<FlowState>();
        }
        await Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // 只处理玩家自己的渐入佳境
        if (power is not FlowState || power.Owner != Owner.Creature || amount <= 0)
            return;

        Flash();
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            cardSource
        );
    }
}