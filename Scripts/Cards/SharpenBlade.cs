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

// 磨刀：若龙舌兰在场且处于蓄力循环，使其获得50层特性；
// 若龙舌兰不在场，或龙舌兰处于攻击循环，则改为获得50层磨刀。升级后75层。
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
        Creature creature = base.Owner.Brother();

        // 判断是否应该给龙舌兰加特性：
        // 条件：龙舌兰存在、存活，且处于蓄力循环（非攻击循环）
        bool giveTrait = creature != null
                         && creature.IsAlive
                         && creature.Monster is Brother brother
                         && !brother.IntendsToAttack;

        if (giveTrait && creature.Monster is Brother traitBrother)
        {
            // 龙舌兰在蓄力循环：对龙舌兰施加特性
            await PowerCmd.Apply<TraitPower>(
                choiceContext,
                creature,
                DynamicVars["TraitAmount"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            // 龙舌兰不存在、死亡，或处于攻击循环：对玩家施加磨刀
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