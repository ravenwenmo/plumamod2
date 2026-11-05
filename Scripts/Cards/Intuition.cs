using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 感应：1费攻击牌，获得1层[渐入佳境]并造成6点伤害。
// 渐入佳境这一批抽到的牌里，每张本能牌伤害+6，每张切割牌攻击段数+1。升级后渐入佳境层数+1（1 → 2）。
[RegisterCard(typeof(PlumaCardPool))]
public class Intuition : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 基础伤害4，抽牌数基础1（升级后+1变为2）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),
        new CardsVar(1)
    };

    // 基础无关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => Enumerable.Empty<CardKeyword>();

    // 悬浮提示：本能、切割关键词与渐入佳境
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.MuscleMemory),
        HoverTipFactory.FromKeyword(MyKeywords.Slashing),
        HoverTipFactory.FromPower<FlowState>()
    };

    public Intuition() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 抽牌效果换成等量的[渐入佳境]：由渐入佳境去抽攻击牌/本能牌
        int flowAmount = DynamicVars.Cards.IntValue;

        // 先清空"渐入佳境最近拿到的牌"，否则风中之羽（阻止抽牌）等情况下会读到上一次的旧记录
        var existingFlow = player.Creature.Powers.OfType<FlowState>().FirstOrDefault();
        existingFlow?.ClearRecentlyDrawn();

        await PowerCmd.Apply<FlowState>(choiceContext, player.Creature, flowAmount, player.Creature, this);

        // 判断的是"渐入佳境这一批实际拿到的牌"（进手牌的 + 透明世界自动打出的）
        var drawnCards = player.Creature.Powers.OfType<FlowState>().FirstOrDefault()?.RecentlyDrawnCards
                         ?? (IReadOnlyList<CardModel>)System.Array.Empty<CardModel>();

        // 统计额外效果
        decimal finalDamage = DynamicVars.Damage.BaseValue;
        int hits = 1;

        foreach (var card in drawnCards)
        {
            if (card.Keywords.Contains(MyKeywords.MuscleMemory))
                finalDamage += 6; // 每张本能牌伤害+6

            if (card.Keywords.Contains(MyKeywords.Slashing))
                hits += 1;        // 每张切割牌段数+1
        }

        // 执行攻击：段数合并为一条攻击命令，保证活力等能力对每段都生效
        await DamageCmd.Attack(finalDamage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitCount(hits)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 升级效果：抽牌数量 +1（1 → 2）
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}