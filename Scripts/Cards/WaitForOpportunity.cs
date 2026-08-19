using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts.Cards;

// 伺机而动：1费罕见能力牌。让龙舌兰获得25层伺机而动。升级后35层。
[RegisterCard(typeof(PlumaCardPool))]
public class WaitForOpportunity : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("Stacks", 25)
    };

    public WaitForOpportunity()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature brother = base.Owner.Brother();

        if (brother == null || !brother.IsAlive)
        {
            // 龙舌兰不在场时无效果（可后续添加缺失动画）
            return;
        }

        await PowerCmd.Apply<WaitForOpportunityPower>(
            choiceContext,
            brother,
            DynamicVars["Stacks"].BaseValue,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Stacks"].UpgradeValueBy(10m); // 25 → 35
    }
}