using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps; // 新增：提供 ValueProp
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 辅料组合包：0费生成技能牌，消耗。选择一张基酒牌并消耗，将对应的鸡尾酒加入手牌；若手牌没有基酒，则获得5点格挡。
// 额外效果：本牌自己抽 DrawBase 张（未升级 0、升级 1）。
// 持有「调酒之韵」（BartenderRhymePower）时打出会再多抽 1 张，那 1 张由能力自己结算，
// 但卡面显示的抽牌数（CardsToDraw）把它算进去，所以未升级显示「抽1张牌」、升级后显示「抽2张牌」。
[RegisterCard(typeof(TokenCardPool))]
public class MixerPack : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = false;

    // 打出本牌会获得格挡：Nimble 附魔（菲涅尔透镜）的筛选条件就是 card.GainsBlock，
    // 原版所有格挡牌都显式声明为 true；同时它也是自动带出「格挡」HoverTip 的开关。
    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Exhaust,
        //MyKeywords.MuscleMemory
    };

    public MixerPack() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(5m, ValueProp.Move),

        // 本牌实际抽几张（效果用）：未升级 0，升级 +1。
        // 和下面显示用的 CardsToDraw 分开：调酒之韵那 1 张由能力自己抽，
        // 这里若也算进来，OnPlay 会跟着多抽，和能力的结算重复。
        ModCardVars.Int("DrawBase", 0),

        // 描述里显示的抽牌数（显示用）：DrawBase + 调酒之韵的额外抽牌，见 DisplayDrawCount。
        // 抽牌数为 0 时，本地化里「抽{N}张牌。」整句会被 choose(0) 隐藏
        // （见 pluma/localization/zhs/cards.json 的 PLUMA_CARD_MIXER_PACK.description）。
        ModCardVars.Computed(
            "CardsToDraw",
            0m,
            static (card, _) => DisplayDrawCount(card),
            static (card, _, _, _) => DisplayDrawCount(card)),

        // 描述里显示的格挡值（显示用）：Block + 物尽其用能力的层数，见 DisplayBlock。
        // 实际结算仍用 Block：物尽其用的额外格挡由能力自己给（AfterCardPlayed），
        // 这里若也算进 Block 就会重复结算。
        ModCardVars.Computed(
            "BlockDisplay",
            5m,
            static (card, _) => DisplayBlock(card),
            static (card, _, _, _) => DisplayBlock(card))
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);

        bool hasBaseSpirit = handPile.Cards.Any(card => card is Gin or Tequila or Whiskey or Rum or Vodka or Brandy);

        // 没有基酒牌可选时，获得 5 点格挡
        if (!hasBaseSpirit)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
            // 仍然继续抽牌（抽牌是独立效果）
            await CardPileCmd.Draw(choiceContext, DynamicVars["DrawBase"].BaseValue, player);
            return;
        }

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

        if (baseCard != null)
        {
            // 消耗选中的基酒牌
            await CardCmd.Exhaust(choiceContext, baseCard);

            // 基酒 -> 鸡尾酒映射
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

        // 抽牌（只抽本牌自己的 DrawBase 张，调酒之韵的额外 1 张由能力结算）
        await CardPileCmd.Draw(choiceContext, DynamicVars["DrawBase"].BaseValue, player);
    }

    protected override void OnUpgrade()
    {
        //DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["DrawBase"].UpgradeValueBy(1m); // 0 → 1

        // 显示变量自己的存储基准也要跟着升级（0 → 1）。
        // 悬浮预览（如升级版调酒师的小壶里的「辅料组合包+」）可能不会调用 UpdateCardPreview，
        // diff() 显示的 PreviewValue 会回退到这里的存储值；不同步就会显示成 0。
        DynamicVars["CardsToDraw"].UpgradeValueBy(1m);
    }

    // ---------- 卡面显示的抽牌数 / 格挡值 ----------

    // 只读计算，不参与结算：
    // · OnPlay 抽的仍然是 DrawBase 张，调酒之韵的额外 1 张由 BartenderRhymePower 自己结算；
    // · OnPlay 给的仍然是 Block 点格挡，物尽其用的额外格挡由 MakeUseOfEverythingPower 自己结算。
    private static decimal DisplayDrawCount(CardModel? card)
    {
        if (card == null)
        {
            return 0m;
        }

        int baseDraw = card.DynamicVars.GetIntOrDefault("DrawBase", 0);
        return baseDraw + (OwnerCreature(card) is { } owner
            ? owner.GetPowerAmount<BartenderRhymePower>()
            : 0m);
    }

    private static decimal DisplayBlock(CardModel? card)
    {
        if (card == null)
        {
            return 0m;
        }

        int baseBlock = card.DynamicVars.GetIntOrDefault("Block", 5);
        return baseBlock + (OwnerCreature(card) is { } owner
            ? owner.GetPowerAmount<MakeUseOfEverythingPower>()
            : 0m);
    }

    // 百科/图鉴里是 canonical（不可变）实例，Owner getter 会先 AssertMutable() 抛异常，
    // 所以必须先判 IsMutable 再读 Owner（与 InstinctCleanse 的写法一致）。
    private static Creature? OwnerCreature(CardModel card)
        => card is MixerPack mixer && mixer.IsMutable ? mixer.Owner?.Creature : null;
}