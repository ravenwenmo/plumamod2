using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 我来就好：消耗稀有技能牌，固有。龙舌兰在场时，回复基础治疗+每失去TraitStepAmount点特性额外回复HealPerTraitStep点生命，清空特性并切换至强化循环；否则抽牌。升级后费用-1。
[RegisterCard(typeof(PlumaCardPool))]
public class IGotThis : ModCardTemplate, ISpiritModeCard
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust,
        CardKeyword.Innate // 固有
    };

    // 动态变量：基础治疗、特性步长、每步额外治疗、不在场抽牌数
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("BaseHeal", 7),
        ModCardVars.Int("TraitStepAmount", 25),
        ModCardVars.Int("HealPerTraitStep", 1),
        ModCardVars.Int("CardsToDraw", 1)
    };

    // 动态描述：根据龙舌兰是否在场显示不同效果文本
    public LocString SpiritModeDescription
    {
        get
        {
            if (!IsMutable || base.Owner == null || base.Owner.PlayerCombatState == null)
            {
                return new LocString("cards", "PLUMA_CARD_I_GOT_THIS.description");
            }

            Creature? brother = base.Owner.Brother();
            return brother != null && brother.IsAlive
                ? new LocString("cards", "PLUMA_CARD_I_GOT_THIS_ALIVE_DESC")
                : new LocString("cards", "PLUMA_CARD_I_GOT_THIS_MISSING_DESC");
        }
    }

    public LocString GetSpiritDescriptionFor(SpiritTargetBranch branch) => SpiritModeDescription;

    public IGotThis()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? brotherCreature = base.Owner.Brother();

        if (brotherCreature != null && brotherCreature.IsAlive && brotherCreature.Monster is Brother brother)
        {
            int traitAmount = brotherCreature.GetPowerAmount<TraitPower>();

            // 清空特性
            if (traitAmount > 0)
            {
                await PowerCmd.Remove<TraitPower>(brotherCreature);
            }

            // 总治疗 = 基础治疗 + (特性层数 / 步长) * 每步额外治疗
            int totalHeal = DynamicVars["BaseHeal"].IntValue
                + (traitAmount / DynamicVars["TraitStepAmount"].IntValue) * DynamicVars["HealPerTraitStep"].IntValue;

            await CreatureCmd.Heal(brotherCreature, totalHeal);

            // 切换至强化循环
            await brother.SwitchToPowerUpIntent();
        }
        else
        {
            // 龙舌兰不在场：抽牌
            await CardPileCmd.Draw(choiceContext, DynamicVars["CardsToDraw"].BaseValue, base.Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BaseHeal"].UpgradeValueBy(1m);
        DynamicVars["HealPerTraitStep"].UpgradeValueBy(3m);
    }
}