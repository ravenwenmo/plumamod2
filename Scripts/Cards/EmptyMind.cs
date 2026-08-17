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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 放空：弃掉所有手牌并洗牌，移除所有渐入佳境，每失去5层获得1能量，抽牌。升级后多抽1张。
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };

    // 动态变量：抽牌数（基础4，升级后5）

    // 卡牌基础数值
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new EnergyVar(1),
        //new CardsVar(2)
    ];

    public EmptyMind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner; // Player 类型
        if (player == null) return;

        // 获取当前渐入佳境层数
        var flowPower = base.Owner.Creature.Powers.OfType<FlowState>().FirstOrDefault();
        int lostFlowAmount = flowPower != null ? (int)flowPower.Amount : 0;

        // 1. 弃掉所有手牌（只弃不抽）
        /*
        var handCards = PileType.Hand.GetPile(player).Cards;
        await CardCmd.DiscardAndDraw(choiceContext, handCards, 0);
        */
        
        // 2. 洗牌
        await CardPileCmd.Shuffle(choiceContext, player);

        // 3. 失去所有渐入佳境
        await PowerCmd.Remove<FlowState>(base.Owner.Creature);

        // 4. 每失去1层，获得1点能量
        int loseAmount = lostFlowAmount;
        if (loseAmount > 0)
        {
            await PlayerCmd.GainEnergy(loseAmount, base.Owner);
        }

        // 5. 抽牌（基础4张，升级后5张）
        await CardPileCmd.Draw(choiceContext,loseAmount, base.Owner);
    }

    // 悬浮提示：渐入佳境（方便查看效果）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}