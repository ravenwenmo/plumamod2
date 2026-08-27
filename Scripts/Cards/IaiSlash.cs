using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;


namespace Pluma.Scripts;

// 居合：3费稀有保留（升级后）切割攻击牌。
// 触发一层放大创伤。
// 在手牌中时：受到敌方怪物伤害前，本次伤害减半并自动向伤害来源打出最左侧一张本牌（每段伤害至多一张），
[RegisterCard(typeof(PlumaCardPool))]
public class IaiSlash : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),   // 主动伤害 6
        ModCardVars.Int("AmplifyStacks", 2)   // 创伤翻倍层数：基础1，升级后2
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing, // 切割
        CardKeyword.Retain   // 保留
    };
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };
    public IaiSlash()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var wound = cardPlay.Target?.GetPower<OpenWoundPower>();
        if (wound != null)
        {
            await wound.TriggerMultiple(choiceContext, 1, (int)DynamicVars["AmplifyStacks"].BaseValue);
        }
        var target = cardPlay.Target;
        if (target != null)
        {
            // 主动伤害
            AttackCommand attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["AmplifyStacks"].UpgradeValueBy(1m);
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}
