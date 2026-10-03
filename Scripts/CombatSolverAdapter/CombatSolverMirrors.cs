#if PLUMA_COMBATSOLVER_ADAPTER
using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors;
using CombatSolver.Engine.InCombat.Mirrors.Hooks;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using CombatSolver.Engine.InCombat.Simulation;
using CombatSolver;
using Pluma.Scripts;
using Pluma.Scripts.Cards;

namespace Pluma.Scripts.CombatSolverAdapter;

/// <summary>
/// pluma 核心机制的 CombatSolver 镜像登记（手册 THIRD_PARTY_ADAPTERS.md §2.1）。
/// 全部在 Mod 初始化时一次性登记（§3.1），运行期不再改动。
///
/// 第一期覆盖：切割连击计数/创伤、创伤自伤触发、居合减伤与自动打出、
/// 渐入佳境（伤害加成、减费、抽牌）、起始卡组 4 张。
/// 未覆盖（后续逐张补）：其余卡牌/Power/遗物、Brother 挡刀（DamageBlockInternal 补丁，手册 §6 封闭开关）。
/// </summary>
internal static class CombatSolverMirrors
{
    public static void RegisterAll()
    {
        RegisterComboAndTrauma();
        RegisterIaiSlash();
        RegisterFlowState();
        RegisterStarterCards();
        RegisterFlowEnginePowers();
        RegisterOtherPowers();
        RegisterRelics();
        RegisterSpiritCards();
    }

    // ==================== 切割连击 + 创伤 ====================

    private static void RegisterComboAndTrauma()
    {
        AfterCardPlayedMirrors.Registry.Register<SlashingComboSingleton>(HandleComboAfterCardPlayed);
        AfterDamageGivenMirrors.Registry.Register<SlashingComboSingleton>(HandleComboAfterDamageGiven);
        Entry.Logger.Info("[鸡煲] 已登记镜像: 切割连击计数 + 创伤施加/自伤触发");
    }

    /// <summary>镜像 SlashingComboSingleton 的连击计数器（每玩家独立，上限 5）。</summary>
    private sealed class SlashingComboPredictionState : IPredictionStateForkable
    {
        public Dictionary<Player, int> Counters { get; set; } = new();

        /// <summary>创伤自伤的防重入标记（对应实机 OpenWoundPower._isApplying）。</summary>
        public bool TraumaApplying { get; set; }

        public object Fork(PredictionForkContext context)
        {
            return new SlashingComboPredictionState
            {
                Counters = new Dictionary<Player, int>(Counters),
                TraumaApplying = TraumaApplying,
            };
        }
    }

    private static void HandleComboAfterCardPlayed(
        SlashingComboSingleton listener,
        AfterCardPlayedMirrorContext context)
    {
        CardModel card = context.CardPlay.Card;
        Player? player = card.Owner;
        if (player == null)
        {
            return;
        }

        var state = context.StateStore.Get(listener, static () => new SlashingComboPredictionState());

        if (card.Keywords.Contains(MyKeywords.Slashing))
        {
            state.Counters.TryGetValue(player, out int current);
            if (current < 5)
            {
                state.Counters[player] = current + 1;
            }
        }
        else if (!HasPredictedPower<PrecisionRepetitionPower>(context.CombatState, player.Creature))
        {
            // 利刃形态在场时非切割牌不清零（对应实机逻辑）。
            state.Counters.Remove(player);
        }
    }

    private static void HandleComboAfterDamageGiven(
        SlashingComboSingleton listener,
        AfterDamageGivenMirrorContext context)
    {
        var state = context.StateStore.Get(listener, static () => new SlashingComboPredictionState());

        // 1) 创伤自伤：实机是 OpenWoundPower 对 CreatureCmd.Damage 的 Harmony 补丁——
        //    镜像里挂在 SlashingComboSingleton 的 AfterDamageGiven 分派上，
        //    语义等价为「持有创伤的单位造成伤害后，先受等量无格挡伤害，再 -1 层」。
        //    已知近似：实机按每次 Damage 命令触发，镜像按每次 AfterDamageGiven 事件触发，
        //    多段/AOE 伤害的触发次数可能不同；全格挡的伤害事件也可能漏触发。
        if (context.Dealer != null && !state.TraumaApplying)
        {
            OpenWoundPower? wound = GetPredictedPower<OpenWoundPower>(context.CombatState, context.Dealer);
            if (wound != null && wound.Amount > 0)
            {
                state.TraumaApplying = true;
                try
                {
                    context.Simulator.Damage(
                        context.Dealer,
                        wound.Amount,
                        ValueProp.Unpowered | ValueProp.Unblockable,
                        context.Dealer);

                    if (context.Simulator.HasPendingChoice)
                    {
                        return;
                    }

                    if (context.CombatState is ICombatPredictionEffectSink effects)
                    {
                        effects.SetPowerAmount(wound, Math.Max(0, wound.Amount - 1));
                        context.StateStore.GetPowerAmount(wound).Decrement();
                    }
                }
                finally
                {
                    state.TraumaApplying = false;
                }
            }
        }

        // 2) 切割牌造成伤害时给目标叠加连击数层创伤（对应 SlashingComboSingleton.AfterDamageGiven）。
        if (context.Source?.Preview is { } cardSource
            && cardSource.Keywords.Contains(MyKeywords.Slashing))
        {
            Player? player = cardSource.Owner;
            if (player == null || !context.Props.IsPoweredAttack())
            {
                return;
            }

            if (state.Counters.TryGetValue(player, out int count) && count > 0
                && context.CombatState is ICombatPredictionEffectSink sink)
            {
                sink.ApplyPower(typeof(OpenWoundPower), context.Target, count, context.Dealer);
            }
        }
    }

    // ==================== 居合 ====================

    private static void RegisterIaiSlash()
    {
        ModifyDamageMirrors.MultiplicativeRegistry.Register<IaiSlashTriggerSingleton>(HandleIaiModifyDamage);
        BeforeDamageReceivedMirrors.Registry.Register<IaiSlashTriggerSingleton>(HandleIaiBeforeDamageReceived);
        Entry.Logger.Info("[鸡煲] 已登记镜像: 居合减伤 + 自动打出");
    }

    private sealed class IaiAutoPlayPredictionState : IPredictionStateForkable
    {
        public bool AutoPlaying { get; set; }

        public object Fork(PredictionForkContext context) => MemberwiseClone();
    }

    private static decimal HandleIaiModifyDamage(
        IaiSlashTriggerSingleton listener,
        ModifyDamageMirrorContext context)
    {
        // 对应实机 IaiSlashTriggerSingleton.ModifyDamageMultiplicative：
        // 敌方回合、目标为玩家且手牌有居合时，本次攻击伤害减半。
        if (!context.Props.IsPoweredAttack()
            || context.Target is not { IsPlayer: true }
            || context.Dealer is not { IsMonster: true })
        {
            return 1m;
        }

        if (context.Target.Player == null || context.CombatState.CurrentSide == CombatSide.Player)
        {
            return 1m;
        }

        var hand = context.State.GetPlayerCombatState(context.Target.Player).Hand;
        return hand.Cards.Any(c => c.Preview is IaiSlash) ? 0.5m : 1m;
    }

    private static void HandleIaiBeforeDamageReceived(
        IaiSlashTriggerSingleton listener,
        BeforeDamageReceivedMirrorContext context)
    {
        // 对应实机 IaiSlashTriggerSingleton.BeforeDamageReceived：自动打出手牌中的居合。
        if (!IaiSlashTriggerSingleton.CanTrigger(context.Target, context.Dealer)
            || context.Target.Player == null
            || context.CombatState.CurrentSide == CombatSide.Player)
        {
            return;
        }

        var hand = context.State.GetPlayerCombatState(context.Target.Player).Hand;
        PredictedCard? iai = hand.Cards.FirstOrDefault(c => c.Preview is IaiSlash);
        if (iai == null)
        {
            return;
        }

        var state = context.StateStore.Get(listener, static () => new IaiAutoPlayPredictionState());
        if (state.AutoPlaying)
        {
            return;
        }

        state.AutoPlaying = true;
        try
        {
            context.Simulator.AutoPlay(iai, context.Dealer);
        }
        finally
        {
            state.AutoPlaying = false;
        }
    }

    // ==================== 渐入佳境（Flow） ====================

    private static void RegisterFlowState()
    {
        ModifyDamageMirrors.MultiplicativeRegistry.Register<FlowState>(HandleFlowModifyDamage);
        ModifyEnergyCostInCombatMirrors.LateRegistry.Register<FlowState>(HandleFlowEnergyCostLate);
        BeforeCardPlayedMirrors.Registry.Register<FlowState>(HandleFlowBeforeCardPlayed);
        Entry.Logger.Info("[鸡煲] 已登记镜像: 渐入佳境伤害加成/本能减费/出牌清理");
    }

    private sealed class FlowPredictionState : IPredictionStateForkable
    {
        /// <summary>本回合被临时减费的牌（按 Original 卡身份存，跨 Fork 稳定）。</summary>
        public HashSet<CardModel> Discounted { get; set; } = new();

        /// <summary>减费生效的回合号，防止跨回合泄漏（实机在回合末清空）。</summary>
        public int DiscountedTurn { get; set; } = -1;

        public object Fork(PredictionForkContext context)
        {
            return new FlowPredictionState
            {
                Discounted = new HashSet<CardModel>(Discounted),
                DiscountedTurn = DiscountedTurn,
            };
        }
    }

    private static decimal HandleFlowModifyDamage(FlowState power, ModifyDamageMirrorContext context)
    {
        // 对应实机：ButFocused 不重复计算；12 层以上攻击 +10% 伤害。
        if (context.CardSource?.Preview is ButFocused)
        {
            return 1m;
        }

        if (context.Dealer == power.Owner
            && context.CardSource?.Preview is { Type: CardType.Attack }
            && power.Amount >= 12
            && context.Props.IsPoweredAttack())
        {
            return 1.1m;
        }

        return 1m;
    }

    private static decimal HandleFlowEnergyCostLate(
        FlowState power,
        ModifyEnergyCostInCombatMirrorContext context)
    {
        if (context.Card.Preview.Owner?.Creature != power.Owner)
        {
            return context.Cost;
        }

        if (context.Card.GetPile(context.State)?.Type is not (PileType.Hand or PileType.Play))
        {
            return context.Cost;
        }

        var state = context.StateStore.Get(power, static () => new FlowPredictionState());
        int currentTurn = GetPredictedTurn(context.Simulator, context.Card.Preview.Owner);
        if (state.DiscountedTurn != currentTurn || !state.Discounted.Contains(context.Card.Original))
        {
            return context.Cost;
        }

        return Math.Max(0m, context.Cost - 1m);
    }

    private static void HandleFlowBeforeCardPlayed(
        FlowState power,
        BeforeCardPlayedMirrorContext context)
    {
        // 对应实机 FlowState.BeforeCardPlayed：本能牌打出时解除临时减费。
        if (context.CardPlay.Card.Owner?.Creature != power.Owner)
        {
            return;
        }

        var state = context.StateStore.Get(power, static () => new FlowPredictionState());
        state.Discounted.Remove(context.Card.Original);
    }

    /// <summary>
    /// 镜像完整的「获得渐入佳境层数」结算链（实机 = FlowState.AfterPowerAmountChanged，
    /// 该 hook 没有镜像分派，属手册 §6 封闭开关，因此在每个施加来源的镜像里内联调用）：
    /// 1) 施加 FlowState；2) 优良机动给格挡；3) 风中之羽在场则跳过抽牌；
    /// 4) 从抽牌堆抽攻击/本能牌（透明确界在场时自动打出本能牌）。
    /// </summary>
    private static void ApplyFlowChain<TBase>(CombatMirrorContext<TBase> context, Player player, int amount)
        where TBase : AbstractModel
    {
        if (context.CombatState is not ICombatPredictionEffectSink effects)
        {
            throw new InvalidOperationException("渐入佳境镜像缺少可写的预测状态。");
        }

        Creature owner = player.Creature;

        // 1) 施加层数。
        effects.ApplyPower(typeof(FlowState), owner, amount, owner);

        // 2) 模组碎片：层数 > 8 时给 2 点力量，回落后收回（实机由 FlowState 主动调用）。
        ModShard? modShard = GetPredictedRelic<ModShard>(context.CombatState, player);
        if (modShard != null)
        {
            var shardState = context.StateStore.Get(
                modShard,
                () => new ModSharedPredictionState(modShard.StrengthApplied));
            int flowStacks = GetPredictedPower<FlowState>(context.CombatState, owner) is { } flow
                ? (int)flow.Amount
                : 0;
            bool shouldHaveStrength = flowStacks > 8;
            if (shouldHaveStrength && !shardState.StrengthApplied)
            {
                effects.ApplyPower(typeof(StrengthPower), owner, 2, owner);
                shardState.StrengthApplied = true;
            }
            else if (!shouldHaveStrength && shardState.StrengthApplied)
            {
                effects.ApplyPower(typeof(StrengthPower), owner, -2, owner);
                shardState.StrengthApplied = false;
            }
        }

        // 3) 菠菜罐头：渐入佳境每次增加 +1 活力（实机是独立监听器的 AfterPowerAmountChanged）。
        SpinachCan? spinachCan = GetPredictedRelic<SpinachCan>(context.CombatState, player);
        if (spinachCan != null)
        {
            effects.ApplyPower(typeof(VigorPower), owner, 1, owner);
        }

        // 4) 优良机动：层数增加时获得等量格挡（实机在抽牌前结算）。
        ExcellentMobilityPower? mobility = GetPredictedPower<ExcellentMobilityPower>(context.CombatState, owner);
        if (mobility != null && mobility.Amount > 0)
        {
            context.Simulator.GainBlock(owner, mobility.Amount, ValueProp.Unpowered);
            if (context.Simulator.HasPendingChoice)
            {
                return;
            }
        }

        // 5) 风中之羽：阻止抽牌（伤害加成等其它效果保留）。
        if (HasPredictedPower<WindFeatherPower>(context.CombatState, owner))
        {
            return;
        }

        // 6) 抽攻击/本能牌。
        var simulator = context.Simulator;
        var pileState = context.State.GetPlayerCombatState(player);
        int remaining = amount;

        while (remaining > 0)
        {
            PredictedCard? target = FindFlowTarget(pileState);
            if (target == null && pileState.DiscardPile.Cards.Count > 0)
            {
                simulator.Shuffle(player);
                target = FindFlowTarget(pileState);
            }

            if (target == null)
            {
                break;
            }

            bool hasKeyword = target.Preview.Keywords.Contains(MyKeywords.MuscleMemory);

            if (hasKeyword && HasPredictedPower<TransparentWorldPower>(context.CombatState, owner))
            {
                // 透明确界：本能牌自动打出（目标由求解器按牌面规则解析）。
                // 已知近似：CounterIntuitive 的自动选牌分支未镜像，记风险。
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                simulator.AutoPlay(target, null);
                if (simulator.HasPendingChoice)
                {
                    return;
                }
            }
            else
            {
                simulator.AddToPile(target, PileType.Hand, CardPilePosition.Bottom);

                if (hasKeyword)
                {
                    // 本能牌入自己手牌并临时减 1 费（回合内）。
                    FlowState? flow = GetPredictedPower<FlowState>(context.CombatState, owner);
                    if (flow != null)
                    {
                        var state = context.StateStore.Get(flow, static () => new FlowPredictionState());
                        state.Discounted.Add(target.Original);
                        state.DiscountedTurn = GetPredictedTurn(simulator, player);
                    }
                }
            }

            remaining--;
        }
    }

    private static PredictedCard? FindFlowTarget(SimPlayerCombatState pileState)
    {
        foreach (PredictedCard card in pileState.DrawPile.Cards)
        {
            if (card.Preview.Type == CardType.Attack
                || card.Preview.Keywords.Contains(MyKeywords.MuscleMemory))
            {
                return card;
            }
        }

        return null;
    }

    // ==================== 起始卡组 ====================

    private static void RegisterStarterCards()
    {
        // 打击：通用攻击镜像（AnyEnemy + Damage 动态变量）。
        CardOnPlayMirrors.Registry.Register<StrikePluma>(
            static (card, context) => GeneralCardMirrors.GeneralAttackOnPlay(card, context));

        // 防御：通用格挡镜像。
        CardOnPlayMirrors.Registry.Register<DefendPluma>(
            static (card, context) => GeneralCardMirrors.GeneralBlockOnPlay(card, context));

        CardOnPlayMirrors.Registry.Register<RapidSlashing>(HandleRapidSlashingOnPlay);
        CardOnPlayMirrors.Registry.Register<BartendersFlask>(HandleBartendersFlaskOnPlay);

        Entry.Logger.Info("[鸡煲] 已登记镜像: 起始卡组（打击/防御/高速切割/调酒师的小壶）");
    }

    private static void HandleRapidSlashingOnPlay(RapidSlashing card, CardOnPlayMirrorContext context)
    {
        if (context.CombatState is not ICombatPredictionEffectSink effects)
        {
            throw new InvalidOperationException("高速切割镜像缺少可写的预测状态。");
        }

        Creature owner = card.Owner.Creature;

        // 施加 1 层高速切割。
        effects.ApplyPower(typeof(RapidSlashingStacks), owner, 1, owner);

        // 达到阈值：移除全部层数，本牌伤害变为 2 段（对应实机 WithHitCount(2)）。
        int hits = 1;
        RapidSlashingStacks? stacks = GetPredictedPower<RapidSlashingStacks>(context.CombatState, owner);
        decimal threshold = card.DynamicVars["Threshold"].BaseValue;
        if (stacks != null && (decimal)stacks.Amount >= threshold)
        {
            effects.SetPowerAmount(stacks, 0);
            context.StateStore.GetPowerAmount(stacks).Consume();
            hits = 2;
        }

        DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(card, context.CardPlay)
            .TargetingAllOpponents(context.CombatState)
            .Simulate(context.Simulator);

        if (context.Simulator.HasPendingChoice)
        {
            return;
        }

        // 获得 1 层渐入佳境，并镜像其完整结算链（优良机动/风中之羽/抽牌/自动打出）。
        ApplyFlowChain(context, card.Owner, 1);
    }

    private static readonly Type[] BaseSpiritCardTypes =
    [
        typeof(Gin), typeof(Tequila), typeof(Whiskey), typeof(Rum), typeof(Vodka), typeof(Brandy),
    ];

    private static void HandleBartendersFlaskOnPlay(BartendersFlask card, CardOnPlayMirrorContext context)
    {
        Player player = card.Owner;
        var handCards = context.State.GetPlayerCombatState(player).Hand.Cards;

        bool hasBaseSpirit = handCards.Any(c =>
            c.Preview is Gin or Tequila or Whiskey or Rum or Vodka or Brandy);

        if (!hasBaseSpirit)
        {
            // 「随机基酒 1」：与实机 BaseSpiritGeneration 逐行对齐（预测 RNG 流）。
            List<PredictedCard> spirits = GenerateRandomBaseSpirits(context, player, 1);
            if (spirits.Count > 0)
            {
                context.Simulator.AddGeneratedCardsToCombat(spirits, PileType.Hand, player);
            }
        }
        else
        {
            // 获得 1 张辅料组合包（升级后为升级版）。
            PredictedCard mixerPack = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
            if (card.IsUpgraded)
            {
                context.Simulator.Upgrade(mixerPack);
            }

            context.Simulator.AddGeneratedCardsToCombat([mixerPack], PileType.Hand, player);
        }
    }

    private static PredictedCard CreatePredictedSpirit(Type type, Player player)
    {
        if (type == typeof(Gin))
        {
            return PredictedCard.Create(CanonicalModels.Card<Gin>(), player);
        }
        if (type == typeof(Tequila))
        {
            return PredictedCard.Create(CanonicalModels.Card<Tequila>(), player);
        }
        if (type == typeof(Whiskey))
        {
            return PredictedCard.Create(CanonicalModels.Card<Whiskey>(), player);
        }
        if (type == typeof(Rum))
        {
            return PredictedCard.Create(CanonicalModels.Card<Rum>(), player);
        }
        if (type == typeof(Vodka))
        {
            return PredictedCard.Create(CanonicalModels.Card<Vodka>(), player);
        }
        return PredictedCard.Create(CanonicalModels.Card<Brandy>(), player);
    }

    // ==================== 渐入佳境引擎相关 Power ====================

    private static void RegisterFlowEnginePowers()
    {
        // 风中之羽：打出攻击牌时获得渐入佳境层数（同时压制 Flow 的抽牌，链内已处理）。
        AfterCardPlayedMirrors.Registry.Register<WindFeatherPower>(HandleWindFeatherAfterCardPlayed);

        // 轻捷如燕：受到伤害时获得渐入佳境层数。
        AfterDamageReceivedMirrors.Registry.Register<LikeABirdPower>(HandleLikeABirdAfterDamageReceived);

        Entry.Logger.Info("[鸡煲] 已登记镜像: 渐入佳境引擎（风中之羽/轻捷如燕/优良机动/透明确界链）");
    }

    private static void HandleWindFeatherAfterCardPlayed(
        WindFeatherPower power,
        AfterCardPlayedMirrorContext context)
    {
        if (context.CardPlay.Card.Owner?.Creature != power.Owner)
        {
            return;
        }
        if (context.PreviewCard.Type != CardType.Attack || power.Amount <= 0)
        {
            return;
        }
        if (context.CardPlay.Card.Owner == null)
        {
            return;
        }

        ApplyFlowChain(context, context.CardPlay.Card.Owner, (int)power.Amount);
    }

    private static void HandleLikeABirdAfterDamageReceived(
        LikeABirdPower power,
        AfterDamageReceivedMirrorContext context)
    {
        // 对应实机：持有者每受到一次伤害，按层数获得渐入佳境。
        if (context.Target != power.Owner || power.Amount <= 0)
        {
            return;
        }
        if (power.Owner.Player == null)
        {
            return;
        }

        ApplyFlowChain(context, power.Owner.Player, (int)power.Amount);
    }

    // ==================== 其它可镜像 Power ====================

    private static void RegisterOtherPowers()
    {
        AfterCardPlayedMirrors.Registry.Register<SlashingDrawPower>(HandleSlashingDrawAfterCardPlayed);
        AfterCardPlayedMirrors.Registry.Register<BartenderRhymePower>(HandleBartenderRhymeAfterCardPlayed);
        AfterCardPlayedMirrors.Registry.Register<MakeUseOfEverythingPower>(HandleMakeUseOfEverythingAfterCardPlayed);
        AfterCardPlayedMirrors.Registry.Register<CocktailGuidePower>(HandleCocktailGuideAfterCardPlayed);
        AfterCardPlayedMirrors.Registry.Register<TheBeatPower>(HandleTheBeatAfterCardPlayed);
        AfterCardPlayedMirrors.Registry.Register<SharpenBladePower>(HandleSharpenBladeAfterCardPlayed);
        ModifyDamageMirrors.MultiplicativeRegistry.Register<SharpenBladePower>(HandleSharpenBladeModifyDamage);
        ModifyDamageMirrors.MultiplicativeRegistry.Register<FlawSensePower>(HandleFlawSenseModifyDamage);
        ModifyDamageMirrors.MultiplicativeRegistry.Register<HarvestTempPower>(HandleHarvestTempModifyDamage);
        ModifyCardPlayCountMirrors.Registry.Register<ChargingPower>(HandleChargingModifyCardPlayCount);
        ModifyCardPlayCountMirrors.AfterRegistry.Register<ChargingPower>(HandleChargingAfterModifyingCardPlayCount);
        AfterDamageGivenMirrors.Registry.Register<ReaperHealingPower>(HandleReaperHealingAfterDamageGiven);
        AfterDamageGivenMirrors.Registry.Register<LiberiPower>(HandleLiberiAfterDamageGiven);
        AfterCardGeneratedForCombatMirrors.Registry.Register<InstantPairPower>(HandleInstantPairAfterCardGeneratedForCombat);
        AfterCardGeneratedForCombatMirrors.Registry.Register<InstantPairUpgradedPower>(HandleInstantPairUpgradedAfterCardGeneratedForCombat);

        Entry.Logger.Info(
            "[鸡煲] 已登记镜像: 切割抽牌/调酒师韵/物尽其用/鸡尾酒指引/节奏感/磨刀/破绽感知/丰收/蓄力/收割回血/黎博利/顺手成双(+升级)");
    }

    private static void HandleSlashingDrawAfterCardPlayed(
        SlashingDrawPower power,
        AfterCardPlayedMirrorContext context)
    {
        // 只响应持有者打出的切割（精确类型匹配，与实机一致）。
        if (context.CardPlay.Card.Owner?.Creature != power.Owner)
        {
            return;
        }
        if (context.CardPlay.Card.GetType() != typeof(Slashing) || power.Amount <= 0)
        {
            return;
        }
        if (context.CardPlay.Card.Owner == null)
        {
            return;
        }

        context.Simulator.Draw(context.CardPlay.Card.Owner, (int)power.Amount);
    }

    private static void HandleBartenderRhymeAfterCardPlayed(
        BartenderRhymePower power,
        AfterCardPlayedMirrorContext context)
    {
        if (context.CardPlay.Card.Owner?.Creature == power.Owner
            && context.PreviewCard is MixerPack
            && context.CardPlay.Card.Owner != null)
        {
            context.Simulator.Draw(context.CardPlay.Card.Owner, 1);
        }
    }

    private static void HandleMakeUseOfEverythingAfterCardPlayed(
        MakeUseOfEverythingPower power,
        AfterCardPlayedMirrorContext context)
    {
        if (context.CardPlay.Card.Owner?.Creature == power.Owner
            && context.PreviewCard is MixerPack)
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleCocktailGuideAfterCardPlayed(
        CocktailGuidePower power,
        AfterCardPlayedMirrorContext context)
    {
        CardModel card = context.CardPlay.Card;
        if (!card.Keywords.Contains(MyKeywords.Cocktail))
        {
            return;
        }
        if (context.CardPlay.Target == null)
        {
            return;
        }
        if (card.Owner == null)
        {
            return;
        }
        // 目标必须是友方（含自己）。
        if (context.CardPlay.Target.Side != card.Owner.Creature.Side)
        {
            return;
        }

        if (!context.CardPlay.Target.IsPlayer)
        {
            // 龙舌兰（宠物）分支：实机把 1 点能量转为宠物 1 点力量。
            // Brother 宠物属于手册 §6 封闭开关，一期记风险跳过。
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        Player? targetPlayer = context.CardPlay.Target.Player;
        if (targetPlayer != null)
        {
            context.Simulator.GainEnergy(targetPlayer, 1);
        }
    }

    private static void HandleTheBeatAfterCardPlayed(
        TheBeatPower power,
        AfterCardPlayedMirrorContext context)
    {
        // 节奏感：每打出 3 张本能牌获得 1 点能量。
        // 计数字段是普通实例字段，会随 MemberwiseClone 进入预测分支，
        // 根捕获时自然携带实机进度，直接读写预测实例上的字段即可。
        if (context.CardPlay.Card.Owner?.Creature != power.Owner)
        {
            return;
        }
        if (!context.CardPlay.Card.Keywords.Contains(MyKeywords.MuscleMemory)
            || context.CardPlay.Card.Owner == null)
        {
            return;
        }

        power.MuscleMemoryCount++;
        if (power.MuscleMemoryCount >= 3)
        {
            context.Simulator.GainEnergy(context.CardPlay.Card.Owner, 1);
            power.MuscleMemoryCount = 0;
        }
    }

    private static void HandleSharpenBladeAfterCardPlayed(
        SharpenBladePower power,
        AfterCardPlayedMirrorContext context)
    {
        // 磨刀：持有者打出攻击牌后移除该能力。
        if (context.CardPlay.Card.Owner?.Creature != power.Owner
            || context.PreviewCard.Type != CardType.Attack)
        {
            return;
        }

        if (context.CombatState is ICombatPredictionEffectSink effects)
        {
            effects.SetPowerAmount(power, 0);
            context.StateStore.GetPowerAmount(power).Consume();
        }
    }

    private static decimal HandleSharpenBladeModifyDamage(
        SharpenBladePower power,
        ModifyDamageMirrorContext context)
    {
        // 磨刀：攻击伤害 + 层数%（保留预测层数）。
        if (context.Dealer == power.Owner
            && context.CardSource?.Preview is { Type: CardType.Attack }
            && power.Amount > 0)
        {
            return 1m + power.Amount / 100m;
        }

        return 1m;
    }

    private static decimal HandleFlawSenseModifyDamage(
        FlawSensePower power,
        ModifyDamageMirrorContext context)
    {
        // 破绽感知：来自持有创伤单位的攻击伤害减半。
        if (context.Target != power.Owner
            || !context.Props.IsPoweredAttack()
            || context.Dealer == null)
        {
            return 1m;
        }

        OpenWoundPower? wound = GetPredictedPower<OpenWoundPower>(context.CombatState, context.Dealer);
        if (wound == null || wound.Amount <= 0)
        {
            return 1m;
        }

        return power.DynamicVars["DamageReduction"].BaseValue;
    }

    private static decimal HandleHarvestTempModifyDamage(
        HarvestTempPower power,
        ModifyDamageMirrorContext context)
    {
        // 丰收（临时层数）：目标生命低于一半时，丰收牌伤害 + 层数%。
        if (context.CardSource?.Preview is Harvest && context.Target != null)
        {
            SimCreatureState target = context.Simulator.State.GetCreature(context.Target);
            if (target.CurrentHp < target.MaxHp / 2m)
            {
                return 1m + power.Amount / 100m;
            }
        }

        return 1m;
    }

    private static int HandleChargingModifyCardPlayCount(
        ChargingPower power,
        ModifyCardPlayCountMirrorContext context)
    {
        // 蓄力：攻击牌多结算一次。
        if (context.Card.Preview.Owner?.Creature == power.Owner
            && context.Card.Preview.Type == CardType.Attack)
        {
            return context.PlayCount + 1;
        }

        return context.PlayCount;
    }

    private static void HandleChargingAfterModifyingCardPlayCount(
        ChargingPower power,
        AfterModifyingCardPlayCountMirrorContext context)
    {
        // 每次生效后消耗 1 层蓄力。
        if (context.CombatState is ICombatPredictionEffectSink effects)
        {
            effects.SetPowerAmount(power, Math.Max(0, power.Amount - 1));
            context.StateStore.GetPowerAmount(power).Decrement();
        }
    }

    private static void HandleReaperHealingAfterDamageGiven(
        ReaperHealingPower power,
        AfterDamageGivenMirrorContext context)
    {
        // 收割回血：对非自身目标造成伤害后回复层数点生命（实机不要求伤害穿透格挡）。
        if (context.Dealer != power.Owner || context.Target == power.Owner)
        {
            return;
        }

        context.Simulator.Heal(power.Owner, power.Amount);
    }

    private static void HandleLiberiAfterDamageGiven(
        LiberiPower power,
        AfterDamageGivenMirrorContext context)
    {
        // 黎博利：造成穿透格挡的攻击伤害时给目标叠加层数点创伤。
        if (context.Dealer != power.Owner
            || !context.Props.IsPoweredAttack()
            || context.Result.UnblockedDamage <= 0)
        {
            return;
        }

        if (context.CombatState is ICombatPredictionEffectSink effects)
        {
            effects.ApplyPower(typeof(OpenWoundPower), context.Target, power.Amount, power.Owner);
        }
    }

    private static void HandleInstantPairAfterCardGeneratedForCombat(
        InstantPairPower power,
        AfterCardGeneratedForCombatMirrorContext context)
    {
        // 顺手成双：持有者每生成一张基酒，向抽牌堆随机位置塞一张辅料组合包。
        if (context.PreviewCard is not IBaseSpiritCard
            || context.PreviewCard.Owner?.Creature != power.Owner
            || context.PreviewCard.Owner == null)
        {
            return;
        }

        Player player = context.PreviewCard.Owner;
        PredictedCard mixerPack = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
        context.Simulator.AddGeneratedCardToCombat(
            mixerPack,
            PileType.Draw,
            context.Creator,
            CardPilePosition.Random);
    }

    private static void HandleInstantPairUpgradedAfterCardGeneratedForCombat(
        InstantPairUpgradedPower power,
        AfterCardGeneratedForCombatMirrorContext context)
    {
        // 升级版顺手成双：生成基酒时向抽牌堆随机位置塞一张「升级版」辅料组合包。
        if (context.PreviewCard is not IBaseSpiritCard
            || context.PreviewCard.Owner?.Creature != power.Owner
            || context.PreviewCard.Owner == null)
        {
            return;
        }

        Player player = context.PreviewCard.Owner;
        PredictedCard mixerPack = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
        context.Simulator.Upgrade(mixerPack);
        context.Simulator.AddGeneratedCardToCombat(
            mixerPack,
            PileType.Draw,
            context.Creator,
            CardPilePosition.Random);
    }

    // ==================== 遗物 ====================

    private static void RegisterRelics()
    {
        AfterDamageGivenMirrors.Registry.Register<ReaperBadge>(HandleReaperBadgeAfterDamageGiven);
        AfterDamageGivenMirrors.Registry.Register<AquaDawn>(HandleAquaDawnAfterDamageGiven);
        AfterShuffleMirrors.Registry.Register<GoldenPlains>(HandleGoldenPlainsAfterShuffle);
        AfterShuffleMirrors.Registry.Register<PrimaRegola>(HandlePrimaRegolaAfterShuffle);

        // LiberiLens（黎博利镜片）在进战斗房时挂 Power，属根捕获前效果，无需镜像；
        // BrotherRelic 的宠物生成也发生在开战前，宠物行为属 §6 封闭开关（挡刀补丁）。
        Entry.Logger.Info("[鸡煲] 已登记镜像: 遗物（收割者徽章/水色黎明回血、金色平原/第一规则洗牌充能；模组碎片/菠菜罐头已并入渐入佳境链）");
    }

    private static void HandleReaperBadgeAfterDamageGiven(
        ReaperBadge relic,
        AfterDamageGivenMirrorContext context)
    {
        // 收割者徽章：持有者对非自身目标造成伤害后回 1 点血（实机不要求伤害穿透格挡）。
        if (context.Dealer != relic.Owner.Creature || context.Target == relic.Owner.Creature)
        {
            return;
        }

        context.Simulator.Heal(relic.Owner.Creature, 1);
    }

    private static void HandleAquaDawnAfterDamageGiven(
        AquaDawn relic,
        AfterDamageGivenMirrorContext context)
    {
        // 水色黎明：同上回血；若玩家满血且龙舌兰宠物存活，则改为给宠物回血。
        Creature ownerCreature = relic.Owner.Creature;
        if (context.Dealer != ownerCreature || context.Target == ownerCreature)
        {
            return;
        }

        SimCreatureState ownerState = context.Simulator.State.GetCreature(ownerCreature);
        if (ownerState.CurrentHp >= ownerState.MaxHp)
        {
            Creature? brother = context.State.Allies.FirstOrDefault(c => c.Monster is Brother && c.IsAlive);
            if (brother != null)
            {
                context.Simulator.Heal(brother, 1);
                return;
            }

            // 预测侧没有宠物时按回血给玩家近似，并记风险。
            context.Simulator.Heal(ownerCreature, 1);
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        context.Simulator.Heal(ownerCreature, 1);
    }

    private static void HandleGoldenPlainsAfterShuffle(
        GoldenPlains relic,
        AfterShuffleMirrorContext context)
    {
        // 金色平原：持有者每洗 2 次牌获得 1 点能量（进战斗房清零发生在根捕获前）。
        if (context.Player != relic.Owner)
        {
            return;
        }

        relic.ShuffleCount++;
        if (relic.ShuffleCount >= 2)
        {
            relic.ShuffleCount = 0;
            context.Simulator.GainEnergy(relic.Owner, 1);
        }
    }

    private static void HandlePrimaRegolaAfterShuffle(
        PrimaRegola relic,
        AfterShuffleMirrorContext context)
    {
        // 第一规则：同金色平原，每 2 次洗牌获得 2 点能量。
        if (context.Player != relic.Owner)
        {
            return;
        }

        relic.ShuffleCount++;
        if (relic.ShuffleCount >= 2)
        {
            relic.ShuffleCount = 0;
            context.Simulator.GainEnergy(relic.Owner, 2);
        }
    }

    /// <summary>模组碎片「力量是否已施加」的预测状态（实机为 SavedProperty）。</summary>
    private sealed class ModSharedPredictionState : IPredictionStateForkable
    {
        public bool StrengthApplied { get; set; }

        public ModSharedPredictionState(bool strengthApplied)
        {
            StrengthApplied = strengthApplied;
        }

        public object Fork(PredictionForkContext context) => MemberwiseClone();
    }

    // ==================== 卡牌：基酒/随机基酒家族 + 切割/居合 ====================

    private static void RegisterSpiritCards()
    {
        // 切割（代币）：通用 AOE 攻击镜像。
        CardOnPlayMirrors.Registry.Register<Slashing>(
            static (card, context) => GeneralCardMirrors.GeneralAttackOnPlay(card, context));

        CardOnPlayMirrors.Registry.Register<IaiSlash>(HandleIaiSlashOnPlay);
        CardOnPlayMirrors.Registry.Register<DrinkSomething>(HandleDrinkSomethingOnPlay);
        CardOnPlayMirrors.Registry.Register<DrinksForTwo>(HandleDrinksForTwoOnPlay);
        CardOnPlayMirrors.Registry.Register<RandomMixologySet>(HandleRandomMixologySetOnPlay);
        CardOnPlayMirrors.Registry.Register<AllIn>(HandleAllInOnPlay);
        CardOnPlayMirrors.Registry.Register<MixerPack>(HandleMixerPackOnPlay);

        CardOnPlayMirrors.Registry.Register<Gin>(HandleGinOnPlay);
        CardOnPlayMirrors.Registry.Register<Tequila>(HandleTequilaOnPlay);
        CardOnPlayMirrors.Registry.Register<Whiskey>(HandleWhiskeyOnPlay);
        CardOnPlayMirrors.Registry.Register<Rum>(HandleRumOnPlay);
        CardOnPlayMirrors.Registry.Register<Vodka>(HandleVodkaOnPlay);
        CardOnPlayMirrors.Registry.Register<Brandy>(HandleBrandyOnPlay);

        Entry.Logger.Info(
            "[鸡煲] 已登记镜像: 卡牌（切割/居合/喝点什么/两人对饮/随机调酒套装/孤注一掷/辅料组合包/六种基酒）");
    }

    // ---------- 随机基酒共享实现（与 BaseSpiritGeneration 逐行对齐） ----------

    private static List<PredictedCard> GenerateRandomBaseSpirits(
        CardOnPlayMirrorContext context,
        Player player,
        int count)
    {
        List<PredictedCard> generated = [];
        if (count <= 0 || player == null)
        {
            return generated;
        }

        var handCards = context.State.GetPlayerCombatState(player).Hand.Cards;
        var handSpiritTypes = handCards
            .Where(c => c.Preview is IBaseSpiritCard)
            .Select(c => c.Preview.GetType())
            .ToHashSet();

        bool handHasAllSix = BaseSpiritCardTypes.All(handSpiritTypes.Contains);
        List<Type> available = (handHasAllSix
            ? BaseSpiritCardTypes
            : BaseSpiritCardTypes.Where(t => !handSpiritTypes.Contains(t))).ToList();

        var generatedTypes = new HashSet<Type>();
        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0)
            {
                available = BaseSpiritCardTypes.Where(t => !generatedTypes.Contains(t)).ToList();
                if (available.Count == 0)
                {
                    available = BaseSpiritCardTypes.ToList();
                }
            }

            int index = context.Rng.CombatCardGeneration.NextInt(available.Count);
            Type spiritType = available[index];
            available.RemoveAt(index);
            generatedTypes.Add(spiritType);
            generated.Add(CreatePredictedSpirit(spiritType, player));
        }

        return generated;
    }

    private static void HandleIaiSlashOnPlay(IaiSlash card, CardOnPlayMirrorContext context)
    {
        if (context.CombatState is not ICombatPredictionEffectSink effects)
        {
            throw new InvalidOperationException("居合镜像缺少可写的预测状态。");
        }

        Creature? target = context.CardPlay.Target;
        if (target != null)
        {
            // 目标有创伤：先触发一次创伤（层数 × Amplify 无格挡伤害，然后 -1 层）。
            OpenWoundPower? wound = GetPredictedPower<OpenWoundPower>(context.CombatState, target);
            if (wound != null && wound.Amount > 0)
            {
                int amplify = card.DynamicVars["AmplifyStacks"].IntValue;
                context.Simulator.Damage(
                    target,
                    wound.Amount * amplify,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }

                effects.SetPowerAmount(wound, Math.Max(0, wound.Amount - 1));
                context.StateStore.GetPowerAmount(wound).Decrement();
            }

            // 主动攻击伤害。
            DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
                .FromCard(card, context.CardPlay)
                .Targeting(target)
                .Simulate(context.Simulator);
        }
    }

    private static void HandleDrinkSomethingOnPlay(DrinkSomething card, CardOnPlayMirrorContext context)
    {
        Player player = card.Owner;
        List<PredictedCard> spirits = GenerateRandomBaseSpirits(context, player, 1);
        if (spirits.Count > 0)
        {
            context.Simulator.AddGeneratedCardsToCombat(spirits, PileType.Hand, player);
        }

        context.Simulator.Draw(player, card.DynamicVars.Cards.IntValue);
    }

    private static void HandleDrinksForTwoOnPlay(DrinksForTwo card, CardOnPlayMirrorContext context)
    {
        Player player = card.Owner;
        List<PredictedCard> spirits = GenerateRandomBaseSpirits(context, player, 2);
        if (spirits.Count > 0)
        {
            context.Simulator.AddGeneratedCardsToCombat(spirits, PileType.Hand, player);
        }

        // 两张辅料组合包：抽牌堆随机位置 + 弃牌堆。
        PredictedCard mixerToDraw = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
        PredictedCard mixerToDiscard = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
        if (card.IsUpgraded)
        {
            context.Simulator.Upgrade(mixerToDraw);
            context.Simulator.Upgrade(mixerToDiscard);
        }

        context.Simulator.AddGeneratedCardToCombat(
            mixerToDraw, PileType.Draw, player, CardPilePosition.Random);
        context.Simulator.AddGeneratedCardToCombat(
            mixerToDiscard, PileType.Discard, player);
    }

    private static void HandleRandomMixologySetOnPlay(
        RandomMixologySet card,
        CardOnPlayMirrorContext context)
    {
        Player player = card.Owner;
        List<PredictedCard> spirits = GenerateRandomBaseSpirits(context, player, 1);
        if (spirits.Count > 0)
        {
            context.Simulator.AddGeneratedCardsToCombat(spirits, PileType.Hand, player);
        }

        PredictedCard mixerPack = PredictedCard.Create(CanonicalModels.Card<MixerPack>(), player);
        if (card.IsUpgraded)
        {
            context.Simulator.Upgrade(mixerPack);
        }

        context.Simulator.AddGeneratedCardToCombat(mixerPack, PileType.Discard, player);
    }

    private static void HandleAllInOnPlay(AllIn card, CardOnPlayMirrorContext context)
    {
        int x = context.Card.ResolveEnergyXValue(context.State);
        int amount = card.IsUpgraded ? x + 1 : x;
        if (amount <= 0)
        {
            return;
        }

        List<PredictedCard> spirits = GenerateRandomBaseSpirits(context, card.Owner, amount);
        if (spirits.Count > 0)
        {
            context.Simulator.AddGeneratedCardsToCombat(spirits, PileType.Hand, card.Owner);
        }
    }

    private static void HandleMixerPackOnPlay(MixerPack card, CardOnPlayMirrorContext context)
    {
        Player player = card.Owner;
        var handCards = context.State.GetPlayerCombatState(player).Hand.Cards;
        bool hasBaseSpirit = handCards.Any(c =>
            c.Preview is Gin or Tequila or Whiskey or Rum or Vodka or Brandy);

        if (!hasBaseSpirit)
        {
            // 手牌没有基酒：获得格挡。
            context.Simulator.GainBlock(player.Creature, card.DynamicVars.Block.BaseValue, ValueProp.Move);
        }
        else
        {
            // 选牌分支（消耗一张基酒换对应鸡尾酒）需要 CardChoiceMirrors，
            // 本批未登记：只保留抽牌并记风险。
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }

        // 只抽本牌自己的 DrawBase 张：调酒之韵的额外 1 张由该能力的 AfterCardPlayed 镜像结算。
        // （CardsToDraw 是给卡面显示用的计算变量，不参与结算。）
        context.Simulator.Draw(player, card.DynamicVars["DrawBase"].IntValue);
    }

    // ---------- 六种基酒（Self/Enemy/Ally 三形态，按 SpiritTargeting.Resolve 分支） ----------

    private static void HandleSpiritEnemyBranch(
        CardModel card,
        CardOnPlayMirrorContext context,
        Creature target,
        Creature owner)
    {
        // 敌人分支：攻击伤害 + 1 层虚弱。
        DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
            .FromCard(card, context.CardPlay)
            .Targeting(target)
            .Simulate(context.Simulator);
        if (context.Simulator.HasPendingChoice)
        {
            return;
        }

        if (context.CombatState is ICombatPredictionEffectSink effects)
        {
            effects.ApplyPower(typeof(WeakPower), target, card.DynamicVars["WeakAmount"].IntValue, owner);
        }
    }

    private static void HandleSpiritSelfDamage(CardOnPlayMirrorContext context, Creature owner)
    {
        // Self/Ally 分支的 1 点穿透自伤（对应实机 CreatureCmd.Damage）。
        context.Simulator.Damage(owner, 1, ValueProp.Unblockable | ValueProp.Unpowered, owner);
    }

    private static ICombatPredictionEffectSink Effects(CardOnPlayMirrorContext context)
        => context.CombatState as ICombatPredictionEffectSink
            ?? throw new InvalidOperationException("基酒镜像缺少可写的预测状态。");

    private static void HandleGinOnPlay(Gin card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;
        var effects = Effects(context);

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                effects.ApplyPower(typeof(FlexPotionPower), owner, card.DynamicVars["FlexAmount"].IntValue, owner);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                effects.ApplyPower(typeof(FlexPotionPower), target, card.DynamicVars["FlexAmount"].IntValue, owner);
                break;
        }
    }

    private static void HandleTequilaOnPlay(Tequila card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;
        var effects = Effects(context);

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                effects.ApplyPower(typeof(RegenPower), owner, card.DynamicVars["RegenAmount"].IntValue, owner);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                if (target.IsPet)
                {
                    // 龙舌兰宠物回春属封闭开关：记风险跳过。
                    context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                }
                else
                {
                    effects.ApplyPower(typeof(RegenPower), target, card.DynamicVars["RegenAmount"].IntValue, owner);
                }
                break;
        }
    }

    private static void HandleWhiskeyOnPlay(Whiskey card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                context.Simulator.GainBlock(owner, card.DynamicVars.Block.BaseValue, ValueProp.Move);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                decimal block = target.IsPet
                    ? card.DynamicVars["PetBlockAmount"].BaseValue
                    : card.DynamicVars.Block.BaseValue;
                context.Simulator.GainBlock(target, block, ValueProp.Move);
                break;
        }
    }

    private static void HandleRumOnPlay(Rum card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;
        var effects = Effects(context);

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                context.Simulator.Draw(card.Owner, card.DynamicVars.Cards.IntValue);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                if (target.IsPet)
                {
                    // 龙舌兰额外攻击段数属封闭开关：记风险跳过。
                    context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                }
                else
                {
                    effects.ApplyPower(
                        typeof(TemporaryExtraHitsPower),
                        target,
                        card.DynamicVars["ExtraHitsAmount"].IntValue,
                        owner);
                }
                break;
        }
    }

    private static void HandleVodkaOnPlay(Vodka card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                context.Simulator.GainEnergy(card.Owner, 2);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                if (target.IsPet)
                {
                    // 龙舌兰特性属封闭开关：记风险跳过。
                    context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                }
                else if (target.Player != null)
                {
                    context.Simulator.GainEnergy(target.Player, 2);
                }
                break;
        }
    }

    private static void HandleBrandyOnPlay(Brandy card, CardOnPlayMirrorContext context)
    {
        Creature owner = card.Owner.Creature;
        Creature? target = context.CardPlay.Target;
        var effects = Effects(context);

        switch (SpiritTargeting.Resolve(target, owner))
        {
            case SpiritTargetBranch.Self:
                HandleSpiritSelfDamage(context, owner);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                effects.ApplyPower(
                    typeof(SpeedPotionPower), owner, card.DynamicVars["SpeedPotionAmount"].IntValue, owner);
                break;

            case SpiritTargetBranch.Enemy:
                if (target != null)
                {
                    HandleSpiritEnemyBranch(card, context, target, owner);
                }
                break;

            case SpiritTargetBranch.Ally:
                if (target == null)
                {
                    return;
                }
                HandleSpiritSelfDamage(context, target);
                if (context.Simulator.HasPendingChoice)
                {
                    return;
                }
                if (target.IsPet)
                {
                    // 龙舌兰覆甲/特性属封闭开关：记风险跳过。
                    context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                }
                else
                {
                    effects.ApplyPower(
                        typeof(SpeedPotionPower), target, card.DynamicVars["SpeedPotionAmount"].IntValue, owner);

                    // 随机消耗一张非保留手牌（战斗目标随机源，与实机同一 RNG 流）。
                    List<PredictedCard> nonRetain = context.State
                        .GetPlayerCombatState(target.Player)
                        .Hand.Cards
                        .Where(c => !c.Preview.Keywords.Contains(CardKeyword.Retain))
                        .ToList();
                    if (nonRetain.Count > 0)
                    {
                        int index = context.Rng.CombatCardSelection.NextInt(nonRetain.Count);
                        context.Simulator.Exhaust(nonRetain[index]);
                    }
                }
                break;
        }
    }

    // ==================== 共用辅助 ====================

    private static TPower? GetPredictedPower<TPower>(ICombatState combatState, Creature owner)
        where TPower : PowerModel
    {
        if (combatState is not ICombatPredictionHookListenerSource source)
        {
            return null;
        }

        return source.HookListeners.OfType<TPower>().FirstOrDefault(p => p.Owner == owner);
    }

    private static TRelic? GetPredictedRelic<TRelic>(ICombatState combatState, Player player)
        where TRelic : RelicModel
    {
        if (combatState is not ICombatPredictionHookListenerSource source)
        {
            return null;
        }

        // 遗物的 Owner 是 Player（与 Power 的 Owner 是 Creature 不同）。
        return source.HookListeners.OfType<TRelic>().FirstOrDefault(r => r.Owner == player);
    }

    private static bool HasPredictedPower<TPower>(ICombatState combatState, Creature owner)
        where TPower : PowerModel
    {
        return GetPredictedPower<TPower>(combatState, owner) != null;
    }

    private static int GetPredictedTurn(CombatPredictionSimulator simulator, Player player)
    {
        if (simulator.State.CombatState is SimulatedCombatState combat)
        {
            return combat.GetPlayerTurnNumber(player);
        }

        return 0;
    }
}
#endif
