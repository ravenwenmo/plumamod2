using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Creatures;

using Pluma.Scripts.Monsters;


namespace Pluma.Scripts.Cards;

// 蓄力：若龙舌兰在场，使其获得1层蓄力；否则自身获得1层蓄力。
[RegisterCard(typeof(PlumaCardPool))]
public class Charging : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust
    };

    public Charging()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<ChargingPower>()
    };
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature brother = base.Owner.Brother();

        if (brother != null && brother.IsAlive)
        {
            // 龙舌兰在场：对其施加蓄力
            await PowerCmd.Apply<ChargingPower>(
                choiceContext,
                brother,
                1m,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            // 龙舌兰不在场：保持原效果
            await PowerCmd.Apply<ChargingPower>(
                choiceContext,
                base.Owner.Creature,
                1m,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}