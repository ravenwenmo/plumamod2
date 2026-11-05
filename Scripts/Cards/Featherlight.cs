using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 轻盈：1费罕见技能，本能 + 消耗。消耗一张手牌，手牌上限 -1（本场战斗可叠加），获得 {Energy} 能量。升级后去掉「消耗」。
[RegisterCard(typeof(PlumaCardPool))]
public class Featherlight : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能 + 消耗（「消耗」在升级时由 OnUpgrade 移除）
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        //CardKeyword.Exhaust
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new EnergyVar(2),                    // 获得的能量
        new PowerVar<FeatherlightPower>(1m)  // 手牌上限 -1（可叠加）
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FeatherlightPower>()
    };

    public Featherlight() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 消耗一张手牌（自己选，用本体自带的"选择要消耗的牌"提示语）
        var toExhaust = (await CardSelectCmd.FromHand(
            choiceContext,
            base.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            null,
            this
        )).FirstOrDefault();
        if (toExhaust != null)
        {
            await CardCmd.Exhaust(choiceContext, toExhaust);
        }

        // 2. 手牌上限 -1（可叠加）
        await PowerCmd.Apply<FeatherlightPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["FeatherlightPower"].BaseValue,
            base.Owner.Creature,
            this
        );

        // 3. 获得能量
        await PlayerCmd.GainEnergy((int)DynamicVars.Energy.BaseValue, base.Owner);
    }

    protected override void OnUpgrade()
    {
        // 敲后删去「消耗」
        //RemoveKeyword(CardKeyword.Exhaust);
        base.DynamicVars.Energy.UpgradeValueBy(1m);

    }
}
