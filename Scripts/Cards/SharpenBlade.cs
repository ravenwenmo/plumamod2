using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;
using Pluma.Scripts.Monsters;


namespace Pluma.Scripts.Cards;

// 磨刀：若龙舌兰存在，使其获得50层特性；否则获得50层磨刀。升级后75层。
[RegisterCard(typeof(PlumaCardPool))]
public class SharpenBlade : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SharpenBladePower>(50m),   // 原效果变量
        ModCardVars.Int("TraitAmount", 50)      // 特性层数变量
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust
    };

    public SharpenBlade()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature brother = base.Owner.Brother();

        if (brother != null && brother.IsAlive)
        {
            // 龙舌兰在场：获得特性
            await PowerCmd.Apply<TraitPower>(
                choiceContext,
                brother,
                DynamicVars["TraitAmount"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            // 龙舌兰不在场：保持原效果
            await PowerCmd.Apply<SharpenBladePower>(
                choiceContext,
                base.Owner.Creature,
                DynamicVars["SharpenBladePower"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SharpenBladePower"].UpgradeValueBy(25m); // 50 → 75
        DynamicVars["TraitAmount"].UpgradeValueBy(25m);       // 50 → 75
    }
}