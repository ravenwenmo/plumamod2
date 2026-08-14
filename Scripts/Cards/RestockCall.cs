using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 补货联络：1费罕见技能牌。获得1张辅料组合包，并在接下来的两个回合开始时各获得1张随机基酒。升级后辅料组合包升级。
[RegisterCard(typeof(PlumaCardPool))]
public class RestockCall : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 悬浮提示：显示基酒和辅料组合包预览
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        HoverTipFactory.FromCard<MixerPack>()
    };

    public RestockCall() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 1. 获得一张辅料组合包（升级后为升级版）
        var mixer = base.CombatState.CreateCard<MixerPack>(player);
        if (base.IsUpgraded)
        {
            CardCmd.Upgrade(new List<CardModel> { mixer }, CardPreviewStyle.None);
        }
        await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixer }, PileType.Hand, player);

        // 2. 施加持续两回合的补货能力
        var power = await PowerCmd.Apply<RestockCallPower>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
        if (power != null)
        {
            power.SetTurns(2);
        }
    }

    protected override void OnUpgrade()
    {
        // 效果在 OnPlay 中判断
    }
}