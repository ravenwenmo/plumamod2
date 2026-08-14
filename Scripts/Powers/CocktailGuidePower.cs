using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 调酒指南：每当一张鸡尾酒牌对自己或友方单位释放时，使目标额外获得1点能量。
[RegisterPower]
public class CocktailGuidePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/CocktailGuide.png",
        BigIconPath: "res://pluma/images/powers/CocktailGuide.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (!card.Keywords.Contains(MyKeywords.Cocktail)) return;
        if (cardPlay.Target == null) return;

        // 目标必须为友方（包含自己）
        if (cardPlay.Target.Side != card.Owner.Creature.Side) return;
        // 只处理玩家单位
        if (!cardPlay.Target.IsPlayer) return;

        var targetPlayer = cardPlay.Target.Player;
        if (targetPlayer == null) return;

        await PlayerCmd.GainEnergy(1, targetPlayer);
    }
}