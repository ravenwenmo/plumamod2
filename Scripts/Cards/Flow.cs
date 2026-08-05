using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

[RegisterCard(typeof(PlumaCardPool))]
public class Flow : ModCardTemplate
{
    private const int energyCost = 1;          // 1 费
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;   // 对自己释放
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 用 DynamicVar 定义层数，方便 OnUpgrade 修改
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        ModCardVars.Int("FlowStateAmount", 3)
    ];

    public Flow() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对自己施加 FlowState 能力
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["FlowStateAmount"].BaseValue,   // 3 层（升级后变为 4 层）
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        // 升级后层数从 3 变为 4
        DynamicVars["FlowStateAmount"].UpgradeValueBy(1m);
    }
}