using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts.Cards;

// ALL! IN!：稀有X费技能牌。「随机基酒 X」：获得X张不同的随机基酒，优先排除手牌已有种类；手牌集齐全部6种后忽略限制开始新的一组。升级后数量+1。
[RegisterCard(typeof(PlumaCardPool))]
public class AllIn : ModCardTemplate
{
    private const int energyCost = 0; // X费牌，实际费用由 HasEnergyCostX 控制
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    protected override bool HasEnergyCostX => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public AllIn()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();
        int amount = base.IsUpgraded ? x + 1 : x;
        if (amount <= 0) return;

        var player = base.Owner;

        // 「随机基酒 X」：统一走 BaseSpiritGeneration。
        // 优先排除手牌已有种类；手牌集齐全部 6 种后忽略限制开始新的一组；超过 6 张时自动循环成新组。
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;
        var generated = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, amount, base.CombatState, rng);

        if (generated.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, player);
        }
    }

    // 悬浮提示：「随机基酒」术语解释（对齐原版「召唤」的 static_hover_tips 实现）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        BaseSpiritGeneration.RandomBaseSpiritHoverTip
    };

    protected override void OnUpgrade()
    {
        // 升级效果已在 OnPlay 中通过 base.IsUpgraded 判断，无需额外代码
    }
}