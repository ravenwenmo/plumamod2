using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
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
using MegaCrit.Sts2.Core.Entities.Players;
using Pluma.Scripts.Vfx;
using STS2RitsuLib;

namespace Pluma.Scripts;

// 渐入佳境：层数增加时从抽牌堆抽取攻击牌或本能牌，本能牌临时减1费（回合结束恢复），并根据层数获得攻击力加成。
[RegisterPower]
public class FlowState : ModPowerTemplate
{
    private readonly HashSet<CardModel> _discountedCards = new();

    // 最近一次触发里"渐入佳境实际拿到的牌"（进手牌的和自动打出的都算），
    // 给「感应」这类需要判断"渐入佳境抽到了什么"的牌用。
    private readonly List<CardModel> _recentlyDrawn = new();
    public IReadOnlyList<CardModel> RecentlyDrawnCards => _recentlyDrawn;
    public void ClearRecentlyDrawn() => _recentlyDrawn.Clear();

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

        // 特效：持有「渐入佳境」期间，小人身上一直挂着官方循环光环（纯本地视觉，不参与同步）
        FlowVfx.SyncAura(base.Owner, true);

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
        
        // 如果持有者拥有“优良机动”，则获得格挡（在抽牌前触发）。
        // 一次增加多层时按层数逐次触发：获得 N 层就触发 N 次，每次获得等同于"优良机动"层数的格挡。
        var excellentMobility = base.Owner.Powers.OfType<ExcellentMobilityPower>().FirstOrDefault();
        if (excellentMobility != null && excellentMobility.Amount > 0)
        {
            for (int i = 0; i < (int)amount; i++)
            {
                await CreatureCmd.GainBlock(
                    base.Owner,
                    excellentMobility.Amount,
                    ValueProp.Unpowered,
                    null,
                    fast: true
                );
            }
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
            // 叠满 12 层后 +10% 伤害
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

    // 渐入佳境被移除时（例如被「全速斩击」一次清空层数），把当时还享受 -1 打折的牌转成卡片自身的临时降费。
    // 折扣原本是靠本能力的 TryModifyEnergyCostInCombatLate 动态算的，能力一消失这些牌就会立刻恢复原价，
    // 玩家会觉得"刚刚明明显示 0 费，突然变回 1 费"。转成 EnergyCost 的临时修改后，
    // 回合结束或打出时由本体自动清理（PlayerCombatState.EndOfTurnCleanup / CardModel.AfterCardPlayedCleanup）。
    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (var card in _discountedCards)
        {
            if (card.IsMutable)
            {
                card.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
                // 本体加局部降费不会主动发这个事件，这里补一下，手牌上的费用数字才会立刻刷新。
                card.InvokeEnergyCostChanged();
            }
        }
        _discountedCards.Clear();
        // 能力消失（被清空层数 / 战斗结束）时把光环一起拆掉
        FlowVfx.SyncAura(oldOwner, false);
        return Task.CompletedTask;
    }

    private async Task DrawAttackCard(PlayerChoiceContext choiceContext, decimal amount, bool clearRecent = true)
    {
        if (clearRecent)
        {
            _recentlyDrawn.Clear();
        }

        _isDrawing = true;
        try
        {
            var player = base.Owner.Player;
            if (player == null) return;

            var combatState = player.Creature.CombatState;
            if (combatState == null) return;

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
                    // 手牌满上限时，本体 CardPileCmd.Add 会把新抽到的牌直接塞进弃牌堆（HAND_FULL）。
                    // 这里改成先弃掉手牌里当前费用最高的一张，腾出位置，让渐入佳境抽到的牌真的进手。
                    await MakeRoomInHand(choiceContext, player);

                    await CardPileCmd.Add(targetCard, PileType.Hand);
                    // 渐入佳境是自定义选牌（绕过 CardPileCmd.Draw 的抽牌主体），
                    // 这里在成功把牌加入手牌后补齐正常抽牌在 Add 之后会触发的钩子/事件，
                    // 使“每抽10张牌获得能量”(AutomationPower) 等监听抽牌的能力正确计数。
                    // 顺序与 CardPileCmd.DrawInternal 保持一致：
                    // 历史记录 -> AfterCardDrawn(Early) -> 卡牌自身的 InvokeDrawn -> 抽牌音效。
                    CombatManager.Instance.History.CardDrawn(combatState, targetCard, fromHandDraw: false);
                    await Hook.AfterCardDrawn(combatState, choiceContext, targetCard, fromHandDraw: false);
                    targetCard.InvokeDrawn();
                    NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
                    if (hasKeyword)
                        _discountedCards.Add(targetCard);
                }

                // 不管进手牌还是自动打出，都记进"本批次拿到的牌"
                _recentlyDrawn.Add(targetCard);

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
                await DrawAttackCard(choiceContext, pending, clearRecent: false);
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

    // 手牌已满时弃掉一张腾位置：优先当前费用最高的牌（含所有临时减费后的实际费用），
    // 费用相同时弃最左边那张（遍历顺序就是手牌从左到右，严格大于才替换）。
    // 无法打出的牌（状态牌、诅咒等 Unplayable）视为最高费用，永远最先被弃。
    private static async Task MakeRoomInHand(PlayerChoiceContext choiceContext, Player player)
    {
        var hand = PileType.Hand.GetPile(player).Cards;

        // 必须用 RitsuLib 的实时上限：本模组有改手牌上限的东西（基酒、罗德岛酒吧、轻盈），
        // 而 CardPile.MaxCardsInHand 是本体里写死 10 的静态属性——RitsuLib 只改写游戏本体程序集里的读取点，
        // 不会改写 mod 程序集里的，所以在 mod 代码里直接读它拿到的是 10，不是真实上限。
        int maxHandSize = RitsuLibFramework.GetMaxHandSize(player);
        if (hand.Count < maxHandSize) return;

        CardModel? toDiscard = null;
        int highestCost = int.MinValue;
        foreach (var card in hand)
        {
            int cost = GetDiscardPriorityCost(card);
            if (cost > highestCost)
            {
                highestCost = cost;
                toDiscard = card;
            }
        }

        if (toDiscard != null)
        {
            await CardCmd.Discard(choiceContext, toDiscard);
        }
    }

    private static int GetDiscardPriorityCost(CardModel card)
    {
        if (card.Keywords.Contains(CardKeyword.Unplayable)) return int.MaxValue;
        return card.EnergyCost.GetWithModifiers(CostModifiers.All);
    }
}