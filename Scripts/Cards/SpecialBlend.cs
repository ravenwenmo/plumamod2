using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 特调：消耗两张手牌，将1张升级过的随机牌加入你的手牌。升级后费用-1。
[RegisterCard(typeof(PlumaCardPool))]
public class SpecialBlend : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public SpecialBlend() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 从手牌选择两张牌（仿 TrueGrit 选择逻辑）
        var selected = await CardSelectCmd.FromHand(
            prefs: new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 2),
            context: choiceContext,
            player: base.Owner,
            filter: null,
            source: this
        );

        var toExhaust = selected.ToList();
        if (toExhaust.Count < 2) return;

        // 消耗这两张牌
        foreach (var card in toExhaust)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        // 从角色卡池中随机生成 1 张牌（仿 Stoke 生成逻辑）
        var unlockedCards = base.Owner.Character.CardPool.GetUnlockedCards(
            base.Owner.UnlockState,
            base.Owner.RunState.CardMultiplayerConstraint
        );

        var generated = CardFactory.GetForCombat(
            base.Owner,
            unlockedCards,
            count: 1,
            base.Owner.RunState.Rng.CombatCardGeneration
        ).ToList();

        if (generated.Count == 0) return;

        // 升级这张随机牌
        CardCmd.Upgrade(generated, CardPreviewStyle.None);

        // 加入手牌
        await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, base.Owner);
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    };
    
    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}