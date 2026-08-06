using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Pluma.Scripts;

// 切割：0费生成攻击牌，对所有敌人造成伤害，虚无。
[RegisterCard(typeof(TokenCardPool))]
public class Slashing : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Token;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = false;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(3m, ValueProp.Move) };

    public Slashing() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
        
        
        //连击上创伤
        var history = CombatManager.Instance.History.CardPlaysStarted;
        var previous = history.Reverse()
            .FirstOrDefault(entry => entry.CardPlay != cardPlay)?
            .CardPlay.Card;

        int count = 0;

        foreach (var entry in history.Reverse())
        {
            // 跳过当前正在打出的这张牌
            if (entry.CardPlay.Card == this) continue;
            
            // 如果这张牌带有 SlashingTag，计数加一；否则结束循环
            if (entry.CardPlay.Card.Tags.Any(t => t == PlumaTags.Slashing))
                count++;
            else
                break;
            
        }
        // 对所有可攻击敌人施加创伤（不是 cardPlay.Target!）
        if (count > 0)
        {
            foreach (var enemy in CombatState.HittableEnemies)
            {
                await PowerCmd.Apply<OpenWoundPower>(
                    choiceContext,
                    enemy,
                    count,
                    base.Owner.Creature,
                    this
                );
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
    }

    public static async Task<IEnumerable<CardModel>> CreateInHand(Player owner, int count, ICombatState combatState, Player? creator = null)
    {
        if (count == 0 || CombatManager.Instance.IsOverOrEnding)
            return Array.Empty<CardModel>();

        var cards = new List<CardModel>();
        for (int i = 0; i < count; i++)
            cards.Add(combatState.CreateCard<Slashing>(owner));

        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, creator ?? owner);
        return cards;
    }

    
    
    protected override HashSet<CardTag> CanonicalTags => [
        PlumaTags.Slashing, // 添加自定义tag
        // CardTag.Strike, // 添加原版tag
    ];
    
    public static async Task<CardModel?> CreateInHand(Player owner, ICombatState combatState, Player? creator = null)
    {
        return (await CreateInHand(owner, 1, combatState, creator)).FirstOrDefault();
    }
}