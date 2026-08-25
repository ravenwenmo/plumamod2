using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.ValueProps;


namespace Pluma.Scripts.Cards;

// 你我二人齐上：1费罕见攻击牌。造成2段3点伤害，自己和龙舌兰获得2层临时力量。升级后伤害4×2，临时力量3。
[RegisterCard(typeof(PlumaCardPool))]
public class TogetherWeFight : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 动态变量：伤害、段数、临时力量层数
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(3m, ValueProp.Move),
        ModCardVars.Int("Hits", 2),
        ModCardVars.Int("FlexAmount", 2)
    };

    public TogetherWeFight()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对目标造成多段伤害：段数合并为一条攻击命令（WithHitCount），
        // 保证活力（VigorPower）等每次攻击消耗的能力对每段伤害都生效，
        // 与原版多段牌保持一致。
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .Execute(choiceContext);

        // 自己获得临时力量
        await PowerCmd.Apply<FlexPotionPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["FlexAmount"].BaseValue,
            base.Owner.Creature,
            this
        );

        // 龙舌兰在场时也获得等量临时力量
        Creature? brother = base.Owner.Brother();
        if (brother != null && brother.IsAlive)
        {
            await PowerCmd.Apply<FlexPotionPower>(
                choiceContext,
                brother,
                DynamicVars["FlexAmount"].BaseValue,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);              // 3 → 4
        DynamicVars["FlexAmount"].UpgradeValueBy(1m);       // 2 → 3
    }
}