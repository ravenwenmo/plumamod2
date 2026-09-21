using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 预感：下个玩家回合开始时，获得等同于能力层数的渐入佳境，然后消失。
[RegisterPower]
public class PremonitionPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 层数即获得量

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/PremonitionPower.png",
        BigIconPath: "res://pluma/images/powers/PremonitionPower.png"
    );
    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        // 只在自己回合开始时触发
        if (player != base.Owner.Player) return;

        // 用回合开始时的层数快照结算，而不是当前的 base.Amount：
        // 结算渐入佳境会触发抽牌，持有「通透世界」时抽到的「预感」会被自动打出，
        // 又给这个能力叠上新层数（同一实例合并）。那些新层数属于下个回合，
        // 这里只结算回合开始时已有的层数，剩下的留到下回合再触发。
        // 另外，本回合中途才获得的「预感」（AmountOnTurnStart 仍为 0）不在本次触发。
        int amountAtTurnStart = base.AmountOnTurnStart;
        if (amountAtTurnStart <= 0) return;

        // 获得对应层数的渐入佳境。
        // 注意顺序：必须先结算、再扣层 —— 结算过程中若被自动打出的「预感」叠了层，
        // 后扣才能只扣掉本次结算的部分，把新层数留下。
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner,
            amountAtTurnStart,
            base.Owner,
            null
        );

        // 一次性效果：只消耗本次已结算的层数。
        // 若期间又被自动打出的「预感」叠了层，剩余层数保留；
        // 扣到 0 时 ModifyAmount 会自动把这个能力移除。
        await PowerCmd.ModifyAmount(
            choiceContext,
            this,
            -amountAtTurnStart,
            base.Owner,
            null
        );
    }
}