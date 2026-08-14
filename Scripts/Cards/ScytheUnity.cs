using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 人镰合一：1费本能技能，选择一张手牌，将其变为切割。升级后费用减1。
[RegisterCard(typeof(PlumaCardPool))]
public class ScytheUnity : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon; // 可根据需要调整
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 本能关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust, // 添加原版关键词
        MyKeywords.MuscleMemory
    };

    // 悬浮提示：预览切割牌的效果
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromCard<Slashing>()
    };

    public ScytheUnity() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);
        if (handPile.Cards.Count == 0) return; // 无手牌则不做任何事

        // 让玩家选择一张手牌
        var selectPrompt = new LocString("cards", "PLUMA_CARD_SCYTHE_UNITY.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(selectPrompt, 1),
            filter: null,
            source: this
        );

        var chosenCard = selected.FirstOrDefault();
        if (chosenCard == null) return;

        // 消耗选中的牌
        await CardCmd.Exhaust(choiceContext, chosenCard);

        // 创建一张新的切割牌
        var slashingCard = base.CombatState.CreateCard<Slashing>(player);
        if (slashingCard != null)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { slashingCard }, PileType.Hand, player);
        }
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}