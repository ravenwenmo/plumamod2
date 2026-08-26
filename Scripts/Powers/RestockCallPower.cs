using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Runs;

namespace Pluma.Scripts;

// 补货联络：在接下来的两个自己回合开始时，各「随机基酒 1」。
[RegisterPower]
public class RestockCallPower : ModPowerTemplate
{
    private int _remainingTurns;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/RestockCallPower.png",
        BigIconPath: "res://pluma/images/powers/RestockCallPower.png"
    );

    // 设置持续回合数（应由卡牌打出时赋予 2）
    public void SetTurns(int turns)
    {
        _remainingTurns = turns;
    }

    // 悬浮提示：「随机基酒」术语解释（对齐原版「召唤」的 static_hover_tips 实现）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        BaseSpiritGeneration.RandomBaseSpiritHoverTip
    };

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        if (_remainingTurns <= 0) return;

        // 「随机基酒 1」：统一走 BaseSpiritGeneration（优先排除手牌已有种类）
        var rng = base.Owner.Player.RunState.Rng.CombatCardGeneration;
        var baseSpirits = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, 1, base.CombatState, rng);
        if (baseSpirits.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(baseSpirits, PileType.Hand, player);
        }

        _remainingTurns--;
        if (_remainingTurns <= 0)
        {
            // 回合开始处理完毕后移除能力
            await PowerCmd.Remove(this);
        }
    }
}