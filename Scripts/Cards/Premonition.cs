using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 预感：1费罕见技能，本能。下回合获得2层渐入佳境。升级后改为3层。
[RegisterCard(typeof(PlumaCardPool))]
public class Premonition : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };

    // 下回合获得的渐入佳境层数，基础2，升级后3
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("FlowStateAmount", 2)
    };

    public Premonition() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int amount = DynamicVars["FlowStateAmount"].IntValue;
        await PowerCmd.Apply<PremonitionPower>(
            choiceContext,
            base.Owner.Creature,
            amount,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FlowStateAmount"].UpgradeValueBy(1m); // 2 → 3
    }
}