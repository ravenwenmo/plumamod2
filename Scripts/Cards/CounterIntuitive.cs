using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 反直觉：1费技能牌，从抽牌堆选择1张非攻击且非本能的牌并打出。升级后可选2张。
[RegisterCard(typeof(PlumaCardPool))]
public class CounterIntuitive : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 关键词：本能，消耗
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory,
        CardKeyword.Exhaust
    };

    
    // 动态变量：可选牌数，基础1，升级后+1
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("Choices", 1)
    };

    public CounterIntuitive() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var drawPile = PileType.Draw.GetPile(player);
        if (drawPile.IsEmpty) return;

        int count = DynamicVars["Choices"].IntValue;

        // 使用 filter 参数直接过滤：非攻击，且非本能
        var selected = await CardSelectCmd.FromCombatPile(
            prefs: new CardSelectorPrefs(
                new LocString("cards", "PLUMA_CARD_COUNTER_INTUITIVE.selectPrompt"),
                count
            ),
            context: choiceContext,
            pile: drawPile,
            player: player,
            filter: c => c.Type != CardType.Attack && !c.Keywords.Contains(MyKeywords.MuscleMemory)
        );

        foreach (var card in selected)
        {
            await CardCmd.AutoPlay(choiceContext, card, null);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Choices"].UpgradeValueBy(1m); // 1 → 2
    }
}