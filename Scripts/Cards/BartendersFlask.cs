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

namespace Pluma.Scripts;

// 调酒师的小壶：1费罕见技能，本能。手牌中没有基酒时获得1张随机基酒，否则获得1张辅料组合包。
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
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        HoverTipFactory.FromCard<MixerPack>()
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
            if (base.IsUpgraded)
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
                // 未升级：随机获得一张基酒
                // 多人同步：使用局内确定性随机源（各端同一序列），严禁 new Random()
                var rng = base.Owner.RunState.Rng.CombatCardGeneration;
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
        }
        else
        {
            // 获得一张辅料组合包
            var mixerPack = base.CombatState.CreateCard<MixerPack>(player);
            await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixerPack }, PileType.Hand, player);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果在 OnPlay 中体现：无基酒时由"随机获得"改为"玩家指定"（见 IsUpgraded 分支）
    }
}