using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;

namespace Pluma.Scripts;

// 渐入佳境：层数增加时从抽牌堆抽取攻击牌或本能牌，本能牌临时减1费（回合结束恢复），并根据层数获得攻击力加成。
[RegisterPower]
public class FlowState : ModPowerTemplate
{
    private readonly HashSet<CardModel> _discountedCards = new();
// 放在 FlowState 类的字段区域
    internal static bool AutoSkipCounterSelect = false;
    // 防重入锁及待处理层数
    private bool _isDrawing;
    private decimal _pendingDrawAmount;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/FlowState.png",
        BigIconPath: "res://pluma/images/powers/FlowState.png"
    );
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await base.AfterPowerAmountChanged(choiceContext, power, amount, applier, cardSource);

        if (power != this || amount <= 0) return;

        // 立即通知模组碎片（如果存在）
        var player = base.Owner.Player;
        if (player != null)
        {
            var modShard = player.Relics.OfType<ModShard>().FirstOrDefault();
            if (modShard != null)
            {
                await modShard.CheckAndUpdateStrength();
            }
        }
        
        // 如果持有者拥有“优良机动”，则获得相应层数的格挡（在抽牌前触发）
        var excellentMobility = base.Owner.Powers.OfType<ExcellentMobilityPower>().FirstOrDefault();
        if (excellentMobility != null && excellentMobility.Amount > 0)
        {
            await CreatureCmd.GainBlock(
                base.Owner,
                excellentMobility.Amount,
                ValueProp.Unpowered,
                null,
                fast: true
            );
        }
        
        // 如果持有者拥有“风中之羽”，则不抽牌
        if (base.Owner.HasPower<WindFeatherPower>())
        {
            return; // 阻止抽牌，但保留伤害加成等其它效果
        }

        // 原有的抽牌逻辑（带延迟）
        await Task.Delay(1);
        await DrawAttackCard(choiceContext, amount);
    }
    
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        // 如果伤害来自 ButFocused，本能力不重复计算
        if (cardSource is ButFocused)
            return 1m;

        if (dealer == base.Owner
            && cardSource != null
            && cardSource.Type == CardType.Attack
            && base.Amount >= 12
            && props.IsPoweredAttack())
        {
            // 叠满 12 层后 +50% 伤害
            return 1.1m;
        }

        return 1m;
    }
    /*
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 如果伤害来自 ButFocused，本能力不重复计算
        if (cardSource is ButFocused)
            return 1m;

        if (dealer == base.Owner && cardSource != null && cardSource.Type == CardType.Attack && base.Amount > 0 && props.IsPoweredAttack())
        {
            return 1m + base.Amount * 3m / 100m;
        }
        return 1m;
    }
    */
    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_discountedCards.Contains(card) && card.Owner.Creature == base.Owner)
        {
            modifiedCost = decimal.Max(0, originalCost - 1);
            return true;
        }
        return false;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner)
        {
            _discountedCards.Remove(cardPlay.Card);
        }
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(base.Owner))
        {
            _discountedCards.Clear();
        }
        return Task.CompletedTask;
    }

    private async Task DrawAttackCard(PlayerChoiceContext choiceContext, decimal amount)
    {
        _isDrawing = true;
        try
        {
            var player = base.Owner.Player;
            if (player == null) return;

            var drawPile = PileType.Draw.GetPile(player);
            var discardPile = PileType.Discard.GetPile(player);
            var targetKeyword = MyKeywords.MuscleMemory;
            bool autoPlay = base.Owner.HasPower<TransparentWorldPower>();

            decimal remaining = amount;
            while (remaining > 0)
            {
                CardModel targetCard = FindTargetCard(drawPile, targetKeyword);
                bool hasKeyword = targetCard != null && targetCard.Keywords.Contains(targetKeyword);

                if (targetCard == null && discardPile.Cards.Count > 0)
                {
                    await CardPileCmd.Shuffle(choiceContext, player);
                    targetCard = FindTargetCard(drawPile, targetKeyword);
                    hasKeyword = targetCard != null && targetCard.Keywords.Contains(targetKeyword);
                }

                if (targetCard == null) break; // 没有符合条件的牌了

                if (hasKeyword && autoPlay)
                {
                    if (targetCard is CounterIntuitive)
                    {
                        // 设置标记，让 CounterIntuitive 自动打出时随机选牌
                        AutoSkipCounterSelect = true;
                        await CardCmd.AutoPlay(choiceContext, targetCard, null);
                        AutoSkipCounterSelect = false;
                    }
                    else
                    {
                        await CardCmd.AutoPlay(choiceContext, targetCard, null);
                    }
                }
                else
                {
                    await CardPileCmd.Add(targetCard, PileType.Hand);
                    if (hasKeyword)
                        _discountedCards.Add(targetCard);
                }

                remaining--;
            }
        }
        finally
        {
            // 处理在此期间新增的层数
            decimal pending = _pendingDrawAmount;
            _pendingDrawAmount = 0;
            _isDrawing = false;

            if (pending > 0)
            {
                // 抽完当前待处理层数后，再递归处理新产生的层数
                await DrawAttackCard(choiceContext, pending);
            }
        }
    }

    private CardModel FindTargetCard(CardPile drawPile, CardKeyword keyword)
    {
        foreach (var card in drawPile.Cards)
        {
            if (card.Type == CardType.Attack || card.Keywords.Contains(keyword))
                return card;
        }
        return null;
    }
}