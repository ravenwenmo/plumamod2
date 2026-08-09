using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

[RegisterCard(typeof(PlumaCardPool))]
public class EmptyMind : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能 + 消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    ];

    // 层数变量（基础 4，升级后 5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        ModCardVars.Int("FlowStateAmount", 4)
    ];

    public EmptyMind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        if (player == null) return;

        // 1. 弃掉所有手牌（DiscardAndDraw 抽 0 张即只弃不抽）
        var handCards = PileType.Hand.GetPile(player).Cards;
        await CardCmd.DiscardAndDraw(choiceContext, handCards, 0);

        // 2. 洗牌
        await CardPileCmd.Shuffle(choiceContext, player);

        // 3. 失去所有渐入佳境（直接移除 FlowState 能力）
        await PowerCmd.Remove<FlowState>(base.Owner.Creature);

        // 4. 获得新层数
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["FlowStateAmount"].BaseValue,
            base.Owner.Creature,
            this
        );
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };
    
    protected override void OnUpgrade()
    {
        DynamicVars["FlowStateAmount"].UpgradeValueBy(1m);
    }
}