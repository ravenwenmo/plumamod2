using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 专业吧台：场上每有单位一次性被施加 2 层以上[创伤]时，将一张 辅料组合包 加入手牌。（升级后给升级过的辅料组合包）
[RegisterPower]
public class ProfessionalBarPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/ProfessionalBarPower.png",
        BigIconPath: "res://pluma/images/powers/ProfessionalBarPower.png"
    );

    // 施加创伤的判定放在 AfterPowerAmountChanged 里：
    // 无论创伤来自卡牌、能力还是遗物、施加给谁，只要场上有单位一次性被叠上 2 层以上创伤就会走到这里。
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);

        if (power is not OpenWoundPower) return;
        if (amount < 2) return;   // 只有"多层"（≥2 层）才触发，1 层和递减都不算

        await GiveMixerPack(choiceContext, upgraded: false);
    }

    // 抽出来给升级版复用：升级版把 upgraded 传 true
    public static async Task GiveMixerPack(PlayerChoiceContext choiceContext, Creature owner, bool upgraded)
    {
        var combatState = owner.CombatState;
        var player = owner.Player;
        if (combatState == null || player == null) return;

        var mixer = combatState.CreateCard<MixerPack>(player);
        if (upgraded)
        {
            CardCmd.Upgrade(new List<CardModel> { mixer }, CardPreviewStyle.None);
        }

        await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixer }, PileType.Hand, player);
    }

    // 保留一份实例上的便捷调用
    private Task GiveMixerPack(PlayerChoiceContext choiceContext, bool upgraded)
        => GiveMixerPack(choiceContext, base.Owner, upgraded);
}
