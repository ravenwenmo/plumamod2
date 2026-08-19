using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Pluma.Scripts;

// 标记回收：每当能力持有者（玩家或其召唤物）的主人消耗基酒牌或辅料牌（MixerPack）时，
// 能力持有者获得等于该能力层数的力量。
[RegisterPower]
public class MarkerRecoveryPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 改为可堆叠

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/MarkerRecoveryPower.png",
        BigIconPath: "res://pluma/images/powers/MarkerRecoveryPower.png"
    );

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        // 能力持有者可能是玩家，也可能是召唤物。
        // 召唤物监听其主人；玩家监听自身。
        Player? effectiveOwner = Owner.PetOwner ?? Owner.Player;
        if (effectiveOwner == null || card.Owner != effectiveOwner)
        {
            return;
        }

        // 基酒牌或辅料牌
        if (card is IBaseSpiritCard || card is MixerPack)
        {
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner,
                (decimal)Amount,          // 力量数值 = 当前能力层数
                Owner,
                card
            );
        }
    }
}