using System.Collections.Generic;
using System.Linq;
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

// 斩断：造成7点伤害。如果敌人意图是攻击，给予4层创伤。升级后伤害11，创伤6。
[RegisterCard(typeof(PlumaCardPool))]
public class Interrupt : ModCardTemplate
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

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(7m, ValueProp.Move),
        ModCardVars.Int("OpenWound", 4)
    ];

    public Interrupt() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 和眼部攻击一样的发光逻辑：只要有敌人意图攻击就发光
    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (base.CombatState == null) return false;
            return base.CombatState.HittableEnemies.Any(e => e.Monster?.IntendsToAttack ?? false);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        if (cardPlay.Target?.Monster?.IntendsToAttack == true)
        {
            await PowerCmd.Apply<OpenWoundPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["OpenWound"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };

    
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);        // 7 → 11
        DynamicVars["OpenWound"].UpgradeValueBy(2m);   // 4 → 6
    }
}