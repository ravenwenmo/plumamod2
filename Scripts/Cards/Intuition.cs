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

namespace Pluma.Scripts;

// 感应：0费攻击牌，抽1张牌并造成4点伤害。抽到本能牌伤害+4，抽到切割牌攻击段数+1。升级后获得本能与切割。
[RegisterCard(typeof(PlumaCardPool))]
public class Intuition : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 基础伤害 4，抽牌数 1
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move),
        new CardsVar(1)
    };

    // 基础无关键词，升级后在 OnUpgrade 中添加
    public override IEnumerable<CardKeyword> CanonicalKeywords => Enumerable.Empty<CardKeyword>();

    // 悬浮提示：本能与切割关键词（无论是否升级都显示，方便玩家预览）
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

        // 抽一张牌
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // 找到新加入手牌的牌
        var newCard = handPile.Cards.FirstOrDefault(c => !beforeDraw.Contains(c));
        bool hasMuscleMemory = newCard?.Keywords.Contains(MyKeywords.MuscleMemory) ?? false;
        bool hasSlashing = newCard?.Keywords.Contains(MyKeywords.Slashing) ?? false;

        // 计算最终伤害
        decimal finalDamage = DynamicVars.Damage.BaseValue;
        if (hasMuscleMemory)
            finalDamage += 4; // 本能加成

        // 攻击次数
        int hits = 1;
        if (hasSlashing)
            hits = 2; // 切割加成

        // 执行攻击
        for (int i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(finalDamage)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target!)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(MyKeywords.MuscleMemory);
        AddKeyword(MyKeywords.Slashing);
    }
}