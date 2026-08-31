using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace Pluma.Scripts;

// 调酒师的小壶：1费罕见技能，本能。手牌中没有基酒时「随机基酒 1」，否则获得1张辅料组合包。
[RegisterCard(typeof(PlumaCardPool))]
[RegisterCharacterStarterCard(typeof(PlumaCharacter), 1)]
public class BartendersFlask : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Basic;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );
    /*
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory
    };
    */
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("BaseSpiritAmount", 1)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip,
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };

    public BartendersFlask() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        var handPile = PileType.Hand.GetPile(player);

        bool hasBaseSpirit = handPile.Cards.Any(card =>
            card is Gin or Tequila or Whiskey or Rum or Vodka or Brandy);

        if (!hasBaseSpirit)
        {
            if (false)
            {
                // 升级后：由玩家从六种基酒中选择一种加入手牌。
                // 选择通过 PlayerChoiceSynchronizer 同步：本地玩家弹出选卡界面，
                // 其余玩家等待并应用相同选择，多人下各端一致。
                var options = new List<CardModel>
                {
                    base.CombatState.CreateCard<Gin>(player),
                    base.CombatState.CreateCard<Tequila>(player),
                    base.CombatState.CreateCard<Whiskey>(player),
                    base.CombatState.CreateCard<Rum>(player),
                    base.CombatState.CreateCard<Vodka>(player),
                    base.CombatState.CreateCard<Brandy>(player),
                };
                var selectPrompt = new LocString("cards", "PLUMA_CARD_BARTENDERS_FLASK.selectPrompt");
                var selected = await CardSelectCmd.FromSimpleGrid(
                    context: choiceContext,
                    cardsIn: options,
                    player: player,
                    prefs: new CardSelectorPrefs(selectPrompt, 1));
                var baseSpirit = selected.FirstOrDefault();
                if (baseSpirit != null)
                {
                    await CardPileCmd.AddGeneratedCardsToCombat(new[] { baseSpirit }, PileType.Hand, player);
                }
            }
            else
            {
                // 「随机基酒 1」：统一走 BaseSpiritGeneration。
                // 此分支仅在手牌中没有基酒时进入，因此等价于从 6 种中随机选 1 种。
                // 多人同步：使用局内确定性随机源（各端同一序列），严禁 new Random()
                var rng = base.Owner.RunState.Rng.CombatCardGeneration;
                var baseSpirits = BaseSpiritGeneration.GenerateRandomBaseSpirits(player, DynamicVars["BaseSpiritAmount"].IntValue, base.CombatState, rng);
                if (baseSpirits.Count > 0)
                {
                    await CardPileCmd.AddGeneratedCardsToCombat(new[] { baseSpirits[0] }, PileType.Hand, player);
                }
            }
        }
        else
        {
            
            // 创建辅料组合包，升级后为升级版
            var mixerPack = base.CombatState.CreateCard<MixerPack>(player);
            if (base.IsUpgraded)
            {
                CardCmd.Upgrade(mixerPack);
            }
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixerPack }, PileType.Hand, player);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain); // 升级后获得保留
    }
}