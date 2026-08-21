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

namespace Pluma.Scripts.Cards;

// 我来就好：1费消耗稀有技能牌，固有。龙舌兰在场时，回复5+失去特性/20点生命，清空特性并切换至强化循环；否则抽1张牌。升级后0费。
[RegisterCard(typeof(PlumaCardPool))]
public class IGotThis : ModCardTemplate, ISpiritModeCard
{
    private const int energyCost = 1;
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

    // 动态描述：根据龙舌兰是否在场显示不同效果文本
    public LocString SpiritModeDescription
    {
        get
        {
            // 百科/图鉴里是 canonical（不可变）实例，Owner getter 会先 AssertMutable() 抛
            // CanonicalModelException（发生在 null 判断之前），必须先判 IsMutable 再读 Owner
            //（游戏自身在 GetDescriptionForPile 里就是用 base.IsMutable 守卫 Owner 读取的）。
            // Owner 未初始化（mutable 但无主人）时同样返回通用描述，避免空引用。
            if (!IsMutable || base.Owner == null)
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

            // 基础回复5点，每失去20层特性额外回复1点
            int totalHeal = 5 + traitAmount / 20;
            await CreatureCmd.Heal(brotherCreature, totalHeal);

            // 切换至强化循环
            await brother.SwitchToPowerUpIntent();
        }
        else
        {
            // 龙舌兰不在场：抽一张牌
            await CardPileCmd.Draw(choiceContext, 1m, base.Owner);
        }
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}