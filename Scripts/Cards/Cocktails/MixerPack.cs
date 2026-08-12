using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 辅料组合包：0费生成技能牌，消耗。选择一张基酒牌并消耗，将对应的鸡尾酒加入手牌。
[RegisterCard(typeof(TokenCardPool))]
public class MixerPack : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = false;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public MixerPack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 手牌中没有基酒牌则不做任何事
        var handPile = PileType.Hand.GetPile(player);
        if (!handPile.Cards.Any(card => card is Gin or Tequila or Whiskey or Rum or Vodka or Brandy)) return;

        // 从手牌选择一张基酒牌
        var selectPrompt = new LocString("cards", "PLUMA_CARD_MIXER_PACK.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: player,
            prefs: new CardSelectorPrefs(selectPrompt, 1),
            filter: card => card is Gin or Tequila or Whiskey or Rum or Vodka or Brandy,
            source: this
        );

        var baseCard = selected.FirstOrDefault();
        if (baseCard == null) return;

        // 消耗选中的基酒牌
        await CardCmd.Exhaust(choiceContext, baseCard);

        // 基酒 -> 鸡尾酒映射：CreateCard 仅有泛型重载，无法用 Type 字典直接创建，这里用类型安全的 switch
        CardModel? cocktail = baseCard switch
        {
            Gin     => base.CombatState.CreateCard<GinTonic>(player),
            Tequila => base.CombatState.CreateCard<Margarita>(player),
            Whiskey => base.CombatState.CreateCard<OldFashioned>(player),
            Rum     => base.CombatState.CreateCard<Mojito>(player),
            Vodka   => base.CombatState.CreateCard<Cosmopolitan>(player),
            Brandy  => base.CombatState.CreateCard<Sidecar>(player),
            _       => null
        };

        // 加入手牌
        if (cocktail != null)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { cocktail }, PileType.Hand, player);
        }
    }

    protected override void OnUpgrade()
    {
        // Token牌无升级
    }
}
