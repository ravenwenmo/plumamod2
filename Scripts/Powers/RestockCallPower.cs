using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Runs;

namespace Pluma.Scripts;

// 补货联络：在接下来的两个自己回合开始时，各获得1张随机基酒。
[RegisterPower]
public class RestockCallPower : ModPowerTemplate
{
    private int _remainingTurns;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/RestockCall.png",
        BigIconPath: "res://pluma/images/powers/RestockCall.png"
    );

    // 设置持续回合数（应由卡牌打出时赋予 2）
    public void SetTurns(int turns)
    {
        _remainingTurns = turns;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        if (_remainingTurns <= 0) return;

        // 随机获得一张基酒
        var rng = base.Owner.Player.RunState.Rng.CombatCardGeneration;
        CardModel baseSpirit = rng.NextInt(6) switch
        {
            0 => base.CombatState.CreateCard<Gin>(player),
            1 => base.CombatState.CreateCard<Tequila>(player),
            2 => base.CombatState.CreateCard<Whiskey>(player),
            3 => base.CombatState.CreateCard<Rum>(player),
            4 => base.CombatState.CreateCard<Vodka>(player),
            _ => base.CombatState.CreateCard<Brandy>(player),
        };

        await CardPileCmd.AddGeneratedCardsToCombat(new[] { baseSpirit }, PileType.Hand, player);

        _remainingTurns--;
        if (_remainingTurns <= 0)
        {
            // 回合开始处理完毕后移除能力
            await PowerCmd.Remove(this);
        }
    }
}