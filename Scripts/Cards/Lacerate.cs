using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 划破：造成6点伤害，施加3层创伤。升级后伤害8，创伤4。
[RegisterCard(typeof(PlumaCardPool))]
public class Lacerate : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );
    
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        MyKeywords.Slashing,
    ];
    
    // 动态变量：基础伤害6，创伤层数3
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(6m, ValueProp.Move),
        ModCardVars.Int("OpenWound", 3)
    ];

    public Lacerate() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 再施加创伤层数
        await PowerCmd.Apply<OpenWoundPower>(
            choiceContext,
            cardPlay.Target!,
            DynamicVars["OpenWound"].BaseValue,
            base.Owner.Creature,
            this
        );
    }
    
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };

    
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);      // 伤害 6→8
        DynamicVars["OpenWound"].UpgradeValueBy(1m); // 创伤 3→4
    }
}