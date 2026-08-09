using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 肌肉记忆：1费技能牌，获得8点格挡，为一张手牌添加“本能”关键词。升级后格挡变为11。
[RegisterCard(typeof(PlumaCardPool))]
public class MuscleMemory : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common; // 可根据需要调整
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 格挡变量（基础8，升级后+3）
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new BlockVar(8m, ValueProp.Move)
    };

    // 悬浮提示：显示本能关键词的解释
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.MuscleMemory)
    };

    public MuscleMemory() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得格挡
        await CreatureCmd.GainBlock(
            base.Owner.Creature,
            DynamicVars.Block.BaseValue,
            ValueProp.Move,
            cardPlay
        );

        // 延迟一帧，确保格挡结算完毕（参考 FormalScythe 的延迟）
        await Task.Delay(1);

        // 选择一张手牌添加本能关键词
        var selectPrompt = new LocString("cards", "PLUMA_CARD_MUSCLE_MEMORY_CARD.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: base.Owner,
            prefs: new CardSelectorPrefs(selectPrompt, 1),
            filter: c => !c.Keywords.Contains(MyKeywords.MuscleMemory), // 只显示没有本能关键词的牌
            source: this
        );

        var targetCard = selected.FirstOrDefault();
        if (targetCard != null)
        {
            CardCmd.ApplyKeyword(targetCard, MyKeywords.MuscleMemory);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m); // 格挡 8 → 11
    }
}