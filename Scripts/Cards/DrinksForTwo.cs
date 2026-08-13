using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 我喝两杯：2费罕见技能，本能，消耗。获得2张随机基酒，将1张辅料组合包放入抽牌堆和弃牌堆。升级后费用减1。
[RegisterCard(typeof(PlumaCardPool))]
public class DrinksForTwo : ModCardTemplate
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
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };

    public DrinksForTwo() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        // 多人同步：使用局内确定性随机源（各端同一序列），严禁 new Random()。
        // rng 在循环外创建，循环内每次 NextInt 消耗一次，保证各端序列一致。
        var rng = base.Owner.RunState.Rng.CombatCardGeneration;

        // 获得两张随机基酒
        for (int i = 0; i < 2; i++)
        {
            CardModel baseSpirit = rng.NextInt(6) switch
            {
                0 => base.CombatState.CreateCard<Gin>(player),
                1 => base.CombatState.CreateCard<Tequila>(player),
                2 => base.CombatState.CreateCard<Whiskey>(player),
                3 => base.CombatState.CreateCard<Rum>(player),
                4 => base.CombatState.CreateCard<Vodka>(player),
                _ => base.CombatState.CreateCard<Brandy>(player),
            };

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