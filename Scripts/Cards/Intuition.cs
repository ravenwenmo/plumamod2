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
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 感应：0费攻击牌，抽1张牌并造成4点伤害。每抽到一张本能牌伤害+4，每抽到一张切割牌攻击段数+1。升级后抽2张牌。
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

    // 悬浮提示：本能与切割关键词
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.MuscleMemory),
        HoverTipFactory.FromKeyword(MyKeywords.Slashing)
    };

    public Intuition() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);

        // 记录抽牌前的手牌
        var beforeDraw = new HashSet<CardModel>(handPile.Cards);

        // 抽牌（数量由 Cards 变量决定）
        int drawCount = DynamicVars.Cards.IntValue;
        await CardPileCmd.Draw(choiceContext, drawCount, player);

        // 找出所有新加入手牌的牌
        var newCards = handPile.Cards.Where(c => !beforeDraw.Contains(c)).ToList();

        // 统计额外效果
        decimal finalDamage = DynamicVars.Damage.BaseValue;
        int hits = 1;

        foreach (var card in newCards)
        {
            if (card.Keywords.Contains(MyKeywords.MuscleMemory))
                finalDamage += 6; // 每张本能牌伤害+4

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