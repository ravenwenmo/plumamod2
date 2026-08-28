using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Pluma.Scripts;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 嚼：2费稀有技能，消耗。让龙舌兰获得1层“嚼”：其下一次攻击斩杀敌人时，提升3点最大生命值并消耗该层。升级后费用降为1。
[RegisterCard(typeof(PlumaCardPool))]
public class Nom : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("Amount", 3)
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust  // 消耗
    };

    public Nom()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? brother = base.Owner.Brother();
        if (brother == null || !brother.IsAlive) return;

        await PowerCmd.Apply<NomPower>(
            choiceContext,
            brother,
            DynamicVars["Amount"].BaseValue,
            base.Owner.Creature,
            this
        );
        // 若龙舌兰当前处于强化循环，则让它进入持续1回合的攻击循环
        if (brother.Monster is Brother b && !b.IntendsToAttack)
        {
            await PowerCmd.Apply<BrotherAttackTurnsPower>(
                choiceContext,
                brother,
                1m,
                base.Owner.Creature,
                this
            );
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Amount"].BaseValue += 1;
    }
}