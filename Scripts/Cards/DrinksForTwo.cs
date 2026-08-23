using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts.Cards;

// 我喝两杯：2费罕见技能，本能，消耗。获得2张不同的随机基酒，将1张辅料组合包放入抽牌堆和弃牌堆。升级后费用减1。
[RegisterCard(typeof(PlumaCardPool))]
public class DrinksForTwo : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory, // 本能
        CardKeyword.Exhaust
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        HoverTipFactory.FromCard<MixerPack>()
    };

    public DrinksForTwo() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;

        // 构建六种基酒候选列表
        var availableBaseSpirits = new List<CardModel>
        {
            base.CombatState.CreateCard<Gin>(player),
            base.CombatState.CreateCard<Tequila>(player),
            base.CombatState.CreateCard<Whiskey>(player),
            base.CombatState.CreateCard<Rum>(player),
            base.CombatState.CreateCard<Vodka>(player),
            base.CombatState.CreateCard<Brandy>(player),
        };

        // 随机获得两张不同的基酒
        for (int i = 0; i < 2; i++)
        {
            int index = rng.NextInt(availableBaseSpirits.Count);
            CardModel baseSpirit = availableBaseSpirits[index];
            availableBaseSpirits.RemoveAt(index);

            await CardPileCmd.AddGeneratedCardsToCombat(new[] { baseSpirit }, PileType.Hand, player);
        }

        // 分别将两张辅料组合包放入抽牌堆和弃牌堆
        var mixerToDraw = base.CombatState.CreateCard<MixerPack>(player);
        var mixerToDiscard = base.CombatState.CreateCard<MixerPack>(player);
        await CardPileCmd.Add(mixerToDraw, PileType.Draw);
        await CardPileCmd.Add(mixerToDiscard, PileType.Discard);
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 2费 → 1费
    }
}