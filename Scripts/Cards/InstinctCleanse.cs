using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 本能稀有技能牌：若龙舌兰在场，清空其特性，你获得等量磨刀，并将龙舌兰切换至强化循环（不消耗）。
// 若龙舌兰不在场或死亡，你获得200层磨刀，且本牌消耗。
// 描述根据龙舌兰是否在场自动切换（通过 ISpiritModeCard + SpiritModeDescriptionPatch）。
[RegisterCard(typeof(PlumaCardPool))]
public class InstinctCleanse : ModCardTemplate, ISpiritModeCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory // 本能（无 Exhaust，消耗逻辑在 OnPlay 中手动处理）
    };
    
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<SharpenBladePower>()
    };

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
                return new LocString("cards", "PLUMA_CARD_INSTINCT_CLEANSE.description");
            }

            Creature? brother = base.Owner.Brother();
            return brother != null && brother.IsAlive
                ? new LocString("cards", "PLUMA_CARD_INSTINCT_CLEANSE_ALIVE_DESC")
                : new LocString("cards", "PLUMA_CARD_INSTINCT_CLEANSE_MISSING_DESC");
        }
    }
    public LocString GetSpiritDescriptionFor(SpiritTargetBranch branch) => SpiritModeDescription;

    public InstinctCleanse()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? brotherCreature = base.Owner.Brother();

        if (brotherCreature != null && brotherCreature.IsAlive && brotherCreature.Monster is Brother brother)
        {
            int traitAmount = brotherCreature.GetPowerAmount<TraitPower>();

            if (traitAmount > 0)
            {
                // 清空龙舌兰特性
                await PowerCmd.Remove<TraitPower>(brotherCreature);

                // 自己获得等量磨刀
                await PowerCmd.Apply<SharpenBladePower>(
                    choiceContext,
                    base.Owner.Creature,
                    (decimal)traitAmount,
                    base.Owner.Creature,
                    this
                );
            }

            // 切换至强化循环（若已在强化循环则内部无操作）
            await brother.SwitchToPowerUpIntent();
        }
        else
        {
            // 龙舌兰不在场或死亡：获得200层磨刀并消耗本牌
            await PowerCmd.Apply<SharpenBladePower>(
                choiceContext,
                base.Owner.Creature,
                200m,
                base.Owner.Creature,
                this
            );

            await CardCmd.Exhaust(choiceContext, this);
        }
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 2费 → 1费
    }
}