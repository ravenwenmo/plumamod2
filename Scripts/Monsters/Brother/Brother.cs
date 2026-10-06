using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
namespace Pluma.Scripts.Monsters;

// 龙舌兰（Brother）：与玩家并肩作战的召唤物，意图在"强化循环"与"攻击循环"之间切换。
// 强化循环：每回合开始时获得1点力量，力量达到 STRENGTH_THRESHOLD 后切换为攻击循环。
// 攻击循环：造成群体伤害（段数受 BrotherExtraHitsPower 层数加成），持续 ATTACK_INTENT_TURNS
// 回合后清空力量并切回强化循环。
// 力量、意图、剩余攻击回合与生命值跨战斗持久化（见 BrotherStateData）。
[RegisterMonster]
public class Brother : ModMonsterTemplate
{
    // 基础生命上限
    public const int INITIAL_HP = 40;
    // 强化循环意图每回合获得的特性层数
    private const int TraitPerTurn = 25;

    // 外部效果对每回合特性获取的额外加成（例如白兰地）
    private int _traitPerTurnBonus;
    
    // 特性达到该值后切换为攻击循环意图
    public const int TraitThreshold = 200;
    // 攻击循环意图持续的回合数（变量，可调整）
    public const int ATTACK_INTENT_TURNS = 4;
    // 攻击基础段数（额外段数由 BrotherExtraHitsPower 层数提供）
    public const int ATTACK_BASE_HITS = 1;
    // 每段基础伤害
    public const int BasicDamage = 4;

    public override int MinInitialHp => INITIAL_HP;
    public override int MaxInitialHp => INITIAL_HP;
    public static Vector2 MinOffset => new Vector2(10f, -10f);
	public static Vector2 MaxOffset => new Vector2(50f, -10f);
    public override bool IsHealthBarVisible => true;

    // 攻击循环的最后一击打完后置为 true，表示"该切回强化循环了"，但实际切换推迟到敌方回合结束。
    // 这样攻击循环的最后一回合依然会替玩家格挡伤害（DieForYou），见 FinishPendingAttackCycleEnd。
    // 注意：能力归零时引擎会自动把它移除，所以这段窗口必须靠本标记补上（见 IsInAttackCycle）。
    private bool _pendingPowerUpSwitch;

    // 是否处于攻击循环 —— 唯一真相是身上的 BrotherAttackTurnsPower（层数 = 剩余攻击回合）。
    // 旧实现另外维护了一个 _isAttackIntent 布尔值，两者会脱节并造成两类卡死：
    //   · 布尔=false 而身上残留/缺少能力 → TriggerWhenGainTrait 被门槛挡掉，特性卡在 200 也不进攻击循环；
    //   · 布尔=true 而没有能力 → BrotherAttackTurnsPower.AfterApplied 里的 SwitchToAttackIntent
    //     因布尔已是 true 而静默 return，日志上看不到"进入攻击循环"，但它也不攻击、不清特性。
    // 现在改为派生属性，不再单独维护，任何一处状态变化都由能力本身承载。
    public bool IsInAttackCycle => _pendingPowerUpSwitch || Creature.HasPower<BrotherAttackTurnsPower>();

    // 攻击循环内剩余的攻击回合数（0 表示本循环已打完最后一击，正在等敌方回合结束）
    public int AttackTurnsRemaining => Creature.GetPowerAmount<BrotherAttackTurnsPower>();

    // 攻击循环意图期间为玩家吸收未格挡的攻击伤害（见 BrotherPower / DamageBlockInternalPatch）
    public bool DieForYou => IsInAttackCycle;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/bro/Bro.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);   
    /// <summary>
    /// 增加龙舌兰强化循环每回合获得的特性层数。
    /// 由卡牌等外部效果调用（例如白兰地）。
    /// </summary>
    public void AddTraitPerTurn(int amount)
    {
        _traitPerTurnBonus += amount;
    }
    public int GetCurrentTraitPerTurn()
    {
        return _traitPerTurnBonus +
               (Creature.HasPower<BrotherAoePower>()
                   ? BrotherAoePower.TraitPerTurn
                   : TraitPerTurn);
    }
    public override async Task AfterAddedToRoom()
    {
        int attackTurnsRemaining = BrotherStateData.GetAttackTurnsRemaining(Creature.PetOwner);
        GD.Print($"[Brother] AfterAddedToRoom: 存档剩余攻击回合={attackTurnsRemaining}");

        // 重新入场时清掉上一场战斗遗留的"待切回强化循环"标记
        _pendingPowerUpSwitch = false;
        
        await CreatureCmd.SetMaxHp(Creature, Math.Max(1, BrotherStateData.GetMaxHp(Creature.PetOwner)));
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, BrotherStateData.GetHp(Creature.PetOwner)));

        // 应用待回复生命值
        Player? petOwner = Creature.PetOwner;
        if (petOwner != null)
        {
            int pendingHeal = BrotherStateData.GetPendingHeal(petOwner);
            if (pendingHeal > 0)
            {
                await CreatureCmd.Heal(Creature, pendingHeal);
                BrotherStateData.ClearPendingHeal(petOwner);
            }
        }
        
        await PowerCmd.Apply<BrotherPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);
        await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, attackTurnsRemaining, null, null);
        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetTrait(Creature.PetOwner), null, null);

        await PowerCmd.Apply<MarkerRecoveryPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            10m,
            null,
            null
        );
        
        // 关键：根据持久化数据恢复意图状态机与动画。
        // 判定统一走 IsInAttackCycle（= 身上有没有 BrotherAttackTurnsPower），
        // 不再看旧的 _isAttackIntent 标志，避免"有剩余回合却显示强化意图"这类脱节。
        if (IsInAttackCycle)
        {
            await SwitchToAttackIntent();   // 幂等：Apply 能力时 AfterApplied 多半已经切过一次

            // 入场时动画还停在召唤用的 "Start" 状态（约 1 秒），这里再显式触发一次，
            // 保证 Skill_1_Start / Skill_2_Start 一定被播到（与旧实现一致，勿删）。
            string startAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_Start" : "Skill_1_Start";
            GD.Print($"[Brother] AfterAddedToRoom: 恢复攻击循环，剩余 {AttackTurnsRemaining} 回合，TriggerAnim '{startAnim}'，creatureNode={(NCombatRoom.Instance?.GetCreatureNode(Creature) != null ? "exists" : "null")}");
            await CreatureCmd.TriggerAnim(Creature, startAnim, 0.3f);
        }
        else
        {
            SetMoveImmediate(GetPowerUpIntent());
        }

        NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        if (LocalContext.IsMe(Creature.PetOwner))
        {
            brotherNode?.ToggleIsInteractable(true);
        }
        GD.Print($"[Brother] AfterAddedToRoom: 攻击循环={IsInAttackCycle}, 剩余攻击回合={AttackTurnsRemaining}, 特性={Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}");
    }

    // 测试牌重复打出时：回满生命值并进入攻击循环
    public async Task OrderBrother()
    {
        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);

        // 统一入口：只负责挂上"剩余攻击回合"能力，意图切换交给
        // BrotherAttackTurnsPower.AfterApplied → SwitchToAttackIntent，
        // 避免出现"已切到攻击意图但身上没有该能力"的脱节状态（旧实现就是直接调 SwitchToAttackIntent）。
        if (!IsInAttackCycle)
        {
            await PowerCmd.Apply<BrotherAttackTurnsPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                ATTACK_INTENT_TURNS,
                Creature,
                null
            );
        }
    }

    // 回合开始（由 BrotherSupportPower 驱动）
    public async Task PlayerTurnStart()
    {
        // 意图对账：状态与显示不一致时以状态为准修正（能力被外力移除/添加都能自愈）
        await ReconcileIntent();

        if (IsInAttackCycle)
        {
            GD.Print($"[Brother] 回合开始：攻击循环中，剩余 {AttackTurnsRemaining} 回合，特性 {Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}");
            return;
        }

        int traitGain = GetCurrentTraitPerTurn();

        await PowerCmd.Apply<TraitPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            traitGain,
            Creature,
            null
        );

        GD.Print($"[Brother] 强化循环回合开始：特性 {Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}（本轮 +{traitGain}），攻击循环={IsInAttackCycle}");

        // 兜底：特性已达阈值但没进攻击循环时强制触发一次。
        // 正常情况下特性变化会经 BrotherPower.AfterPowerAmountChanged → TriggerWhenGainTrait 自动切换，
        // 但特性顶到 200 上限后再怎么加都不再产生"有效"变化时，上面这条路可能整场都不再触发，
        // 于是龙舌兰会一直显示 200/200 却停在强化循环里（本次日志里出现过的卡死）。
        await TriggerWhenGainTrait();
    }

    // 让"显示的意图"与"实际状态"对齐（幂等，可随时调用）
    private async Task ReconcileIntent()
    {
        if (IsInAttackCycle)
        {
            if (!IntendsToAttack)
            {
                GD.Print("[Brother] 意图对账：处于攻击循环但显示的是强化意图，补切攻击意图");
                await SwitchToAttackIntent();
            }
        }
        else if (IntendsToAttack)
        {
            GD.Print("[Brother] 意图对账：已不在攻击循环但仍显示攻击意图，切回强化意图");
            await SwitchToPowerUpIntent(force: true);
        }
    }

    // 回合结束（由 BrotherSupportPower 驱动）
    public async Task TakeTurn(PlayerChoiceContext choiceContext)
    {
        // 强化循环期间回合结束无动作
        if (!IsInAttackCycle)
        {
            return;
        }

        // 本循环已打完最后一击（只剩"等敌方回合结束切回强化循环"），不再攻击。
        // 这里也顺手补一次攻击意图显示，避免 SetMoveImmediate 被
        // NextMove.CanTransitionAway 挡掉后一直显示强化意图。
        if (AttackTurnsRemaining <= 0)
        {
            await ReconcileIntent();
            return;
        }

        int hits = ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>();
        decimal damage = BasicDamage;

        string attackAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_Loop" : "Attack";
        await CreatureCmd.TriggerAnim(Creature, attackAnim, 0.3f);

        BrotherAttackTurnsPower? turnsPower = Creature.GetPower<BrotherAttackTurnsPower>();
        if (turnsPower != null)
        {
            await PowerCmd.Decrement(turnsPower);
        }

        GD.Print($"[Brother] 攻击循环结算：造成 {damage} 点伤害 ×{hits} 段，剩余攻击回合 {AttackTurnsRemaining}");

        for (int i = 0; i < hits; i++)
        {
            IReadOnlyList<Creature> aliveOpponents = Creature.CombatState
                .GetOpponentsOf(Creature)
                .Where(c => c.IsAlive)
                .ToList();

            if (aliveOpponents.Count == 0)
            {
                break;
            }

            IReadOnlyList<Creature> targets;
            if (Creature.HasPower<BrotherAoePower>())
            {
                targets = aliveOpponents;
            }
            else
            {
                targets = new[] { aliveOpponents[0] };
            }

            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, Creature);
        }

        if (Creature.HasPower<TemporaryExtraHitsPower>())
        {
            TemporaryExtraHitsPower tempPower = Creature.GetPower<TemporaryExtraHitsPower>();
            if (tempPower != null)
            {
                await tempPower.ConsumeAfterAttack(choiceContext);
            }
        }

        // 注意：能力归零时引擎会自动移除它（PowerModel.ShouldRemoveDueToAmount），
        // 所以这里不能用 HasPower 判断，只能看层数。
        if (AttackTurnsRemaining <= 0)
        {
            // 本轮攻击循环已打完：不立刻切回强化循环，只做标记，
            // 实际切换推迟到敌方回合结束（FinishPendingAttackCycleEnd）——
            // 否则最后一回合攻击一打完就进入强化循环（DieForYou=false），
            // 紧接着的敌方回合里龙舌兰就不再替玩家格挡伤害了。
            _pendingPowerUpSwitch = true;
            GD.Print("[Brother] 攻击循环已打完最后一击，等敌方回合结束后切回强化循环");
        }
    }

    // 敌方回合结束时调用（由 BrotherSupportPower 驱动）：此时敌人已全部攻击完毕。
    // 若本轮攻击循环已打完最后一击，在这里才真正切回强化循环。
    public async Task FinishPendingAttackCycleEnd()
    {
        if (!_pendingPowerUpSwitch)
        {
            return;
        }

        _pendingPowerUpSwitch = false;

        // 只有在特性层数达到阈值时才移除
        if (Creature.GetPowerAmount<TraitPower>() >= TraitThreshold)
        {
            await PowerCmd.Remove<TraitPower>(Creature);
        }

        // force：此刻能力多半已被引擎移除，_pendingPowerUpSwitch 也已清掉，
        // 默认的 IsInAttackCycle 判定会直接 return，所以这里必须强制走完收尾。
        await SwitchToPowerUpIntent(force: true);

        GD.Print($"[Brother] 敌方回合结束，攻击循环收尾完成：特性={Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}, 攻击循环={IsInAttackCycle}");
    }

    // 切换为攻击循环意图（幂等：已经显示攻击意图时不重复播开始动画）
    public async Task SwitchToAttackIntent()
    {
        if (IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetAttackIntent());

        string startAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_Start" : "Skill_1_Start";
        GD.Print($"[Brother] SwitchToAttackIntent: 剩余攻击回合={AttackTurnsRemaining}, TriggerAnim '{startAnim}'");
        await CreatureCmd.TriggerAnim(Creature, startAnim, 0.3f);

        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            doubleIdentity.RefreshCooldown();
        }
    }

    // 切换为强化循环意图。
    // force=true 用于攻击循环收尾（此时能力已被引擎移除，IsInAttackCycle 已为 false）。
    public async Task SwitchToPowerUpIntent(bool force = false)
    {
        if (!force && !IsInAttackCycle)
        {
            return;
        }

        _pendingPowerUpSwitch = false;
        if (Creature.HasPower<BrotherAttackTurnsPower>())
        {
            await PowerCmd.Remove<BrotherAttackTurnsPower>(Creature);
        }

        if (!IntendsToAttack)
        {
            return; // 本来就没显示攻击意图，不需要播收尾动画
        }

        SetMoveImmediate(GetPowerUpIntent());

        string endAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_End" : "Skill_1_End";
        GD.Print($"[Brother] SwitchToPowerUpIntent: TriggerAnim '{endAnim}'");
        await CreatureCmd.TriggerAnim(Creature, endAnim, 0.3f);

        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            await doubleIdentity.TriggerOnPowerUpCycle();
        }
    }

    // 特性变化时调用（BrotherPower.AfterPowerAmountChanged），也由 PlayerTurnStart 兜底调用。
    // 判定统一走 IsInAttackCycle —— 旧实现用 HasPower<BrotherAttackTurnsPower>()，
    // 一旦状态脱节就会出现"特性 200 却永远不进攻击循环"。
    public async Task TriggerWhenGainTrait()
    {
        if (IsInAttackCycle)
        {
            return;
        }

        if (Creature.GetPowerAmount<TraitPower>() >= TraitThreshold)
        {
            int attackTurns = Creature.HasPower<BrotherAoePower>()
                ? BrotherAoePower.AttackIntentTurns
                : ATTACK_INTENT_TURNS;

            GD.Print($"[Brother] 特性达到阈值 {TraitThreshold}，进入攻击循环（{attackTurns} 回合）");

            await PowerCmd.Apply<BrotherAttackTurnsPower>(
                new ThrowingPlayerChoiceContext(),
                Creature,
                attackTurns,
                Creature,
                null
            );
        }
    }
    // 仅用作显示，召唤物不主动执行意图（实际行动由 BrotherSupportPower 在回合钩子中驱动）
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        GD.Print("[Brother] Generating move state machine for Brother");
        // 意图1：强化
        var powerUpIntent = GetPowerUpIntent();

        // 意图2：攻击
        var attackIntent = GetAttackIntent();

        // 状态转换，任何意图之后仅接强化意图，攻击意图由玩家控制
        powerUpIntent.FollowUpState = powerUpIntent;
        attackIntent.FollowUpState = powerUpIntent;

        // 添加2个意图，并且初始意图设成 powerUpIntent
        return new MonsterMoveStateMachine([powerUpIntent, attackIntent], powerUpIntent);
    }

    private MoveState GetPowerUpIntent()
    {
        var powerUpIntent = new MoveState(
            "POWER_UP",
            PowerUpMove,
            new BrotherBuffIntent()
        );

        return powerUpIntent;
    }

    private MoveState GetAttackIntent()
    {
        var attackIntent = new MoveState(
            "ATTACK",
            AttackMove,
            new BrotherAttackIntent(
                BasicDamage,
                () => ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>(),
                Creature.HasPower<BrotherAoePower>(),
                this   // 传给意图：用于在图标右上角显示攻击循环剩余回合
            )            
        );

        return attackIntent;
    }

    private async Task PowerUpMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

    
    /*
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
        GD.Print("[Brother] Generating animator for Brother");
        AnimState summonAnimState = new AnimState("Start");
		AnimState idleAnimState = new AnimState("Idle", isLooping: true);
		AnimState castAnimState = new AnimState("Skill_1_Start");
        
        AnimState castIdleAnimState = new AnimState("Skill_1_Idle", isLooping: true);
		AnimState castAttackAnimState = new AnimState("Attack");
        AnimState castEndAnimState = new AnimState("Skill_1_End");
		AnimState deadAnimState = new AnimState("Die");
        summonAnimState.NextState = idleAnimState;
        summonAnimState.AddBranch("Skill_1_Start", castAnimState);
        idleAnimState.AddBranch("Dead", deadAnimState);
        idleAnimState.AddBranch("Skill_1_Start", castAnimState);
		castAnimState.NextState = castIdleAnimState;
        castIdleAnimState.AddBranch("Attack", castAttackAnimState);
        castIdleAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castIdleAnimState.AddBranch("Dead", deadAnimState);
        castAttackAnimState.AddBranch("Dead", deadAnimState);
		castAttackAnimState.NextState = castIdleAnimState;
        castAttackAnimState.AddBranch("Skill_1_End", castEndAnimState);
        castEndAnimState.AddBranch("Dead", deadAnimState);
		castEndAnimState.NextState = idleAnimState;
		CreatureAnimator creatureAnimator = new CreatureAnimator(summonAnimState, controller);
		return creatureAnimator;
	}*/
public override CreatureAnimator GenerateAnimator(MegaSprite controller)
{
    GD.Print("[Brother] Generating animator for Brother");

    AnimState summonAnimState = new AnimState("Start");
    AnimState idleAnimState = new AnimState("Idle", isLooping: true);

    // Skill_1 系列（普通攻击循环）
    AnimState castAnimState = new AnimState("Skill_1_Start");
    AnimState castIdleAnimState = new AnimState("Skill_1_Idle", isLooping: true);
    AnimState castEndAnimState = new AnimState("Skill_1_End");
    AnimState castAttackAnimState = new AnimState("Attack");

    // Skill_2 系列（范围攻击循环）
    AnimState cast2AnimState = new AnimState("Skill_2_Start");
    AnimState cast2IdleAnimState = new AnimState("Skill_2_Idle", isLooping: true);
    AnimState cast2EndAnimState = new AnimState("Skill_2_End");
    AnimState cast2AttackAnimState = new AnimState("Skill_2_Loop");

    AnimState deadAnimState = new AnimState("Die");

    // 初始与通用分支
    summonAnimState.NextState = idleAnimState;
    // 重要：入场时 AfterAddedToRoom 恢复攻击循环会立刻触发 Skill_1_Start/Skill_2_Start，
    // 此时动画还处于召唤用的 "Start" 状态（约 1 秒）。如果 Start 状态没有这些分支，
    // SetTrigger 会静默丢弃触发器，Start 播完后只会进入通用 Idle。
    summonAnimState.AddBranch("Skill_1_Start", castAnimState);
    summonAnimState.AddBranch("Skill_2_Start", cast2AnimState);
    idleAnimState.AddBranch("Dead", deadAnimState);
    idleAnimState.AddBranch("Skill_1_Start", castAnimState);
    idleAnimState.AddBranch("Skill_2_Start", cast2AnimState);
    GD.Print("[Brother] GenerateAnimator: Start branches Skill_1_Start/Skill_2_Start registered, Skill_1_Start -> Skill_1_Idle, Skill_2_Start -> Skill_2_Idle");

    // Skill_1 状态链
    castAnimState.NextState = castIdleAnimState;
    castIdleAnimState.AddBranch("Attack", castAttackAnimState);
    castIdleAnimState.AddBranch("Skill_1_End", castEndAnimState);
    castIdleAnimState.AddBranch("Dead", deadAnimState);
    castAttackAnimState.NextState = castIdleAnimState;      // 普通攻击后回到 Skill_1_Idle
    castAttackAnimState.AddBranch("Skill_1_End", castEndAnimState);
    castAttackAnimState.AddBranch("Dead", deadAnimState);
    castEndAnimState.NextState = idleAnimState;

    // Skill_2 状态链
    cast2AnimState.NextState = cast2IdleAnimState;
    cast2IdleAnimState.AddBranch("Skill_2_Loop", cast2AttackAnimState);
    cast2IdleAnimState.AddBranch("Skill_2_End", cast2EndAnimState);
    cast2IdleAnimState.AddBranch("Dead", deadAnimState);
    cast2AttackAnimState.NextState = cast2IdleAnimState;   // 范围攻击后回到 Skill_2_Idle
    cast2AttackAnimState.AddBranch("Skill_2_End", cast2EndAnimState);
    cast2AttackAnimState.AddBranch("Dead", deadAnimState);
    cast2EndAnimState.NextState = idleAnimState;

    CreatureAnimator creatureAnimator = new CreatureAnimator(summonAnimState, controller);
    return creatureAnimator;
}
}
