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

    // 攻击循环意图期间为玩家吸收未格挡的攻击伤害（见 BrotherPower / DamageBlockInternalPatch）
    public bool DieForYou { get; set; } = false;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/bro/Bro.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);   
    
    /*
    public override async Task AfterAddedToRoom()
    {
        await CreatureCmd.SetMaxHp(Creature, Math.Max(1, BrotherStateData.GetMaxHp(Creature.PetOwner)));
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, BrotherStateData.GetHp(Creature.PetOwner)));
        await PowerCmd.Apply<BrotherPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);
        await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetAttackTurnsRemaining(Creature.PetOwner), null, null);
        // 额外增加一个被动
        await PowerCmd.Apply<MarkerRecoveryPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            10m,
            null,
            null
        );

        // 恢复特性层数（替换原来的力量恢复）
        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetTrait(Creature.PetOwner), null, null);

        NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        brotherNode?.ToggleIsInteractable(true);
    }
    */
    
    public override async Task AfterAddedToRoom()
    
    {
    
        int attackTurnsRemaining = BrotherStateData.GetAttackTurnsRemaining(Creature.PetOwner);
        GD.Print($"[Brother] AfterAddedToRoom: attackTurnsRemaining={attackTurnsRemaining}, IntendsToAttack={IntendsToAttack}");
        
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
        /*
        await PowerCmd.Apply<MarkerRecoveryPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            10m,
            null,
            null
        );
        */
        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, BrotherStateData.GetTrait(Creature.PetOwner), null, null);
        GD.Print($"[Brother] AfterAddedToRoom: attackTurnsRemaining={attackTurnsRemaining}, IntendsToAttack={IntendsToAttack}");
        
        
        
        // 关键：根据持久化数据恢复意图状态机与动画
        if (attackTurnsRemaining > 0)
        {
            SetMoveImmediate(GetAttackIntent());   // 先切换状态机，让 IntendsToAttack 变成 true
            DieForYou = true;
            GD.Print($"[Brother] AfterAddedToRoom: attackTurnsRemaining={attackTurnsRemaining}, IntendsToAttack={IntendsToAttack}");
            // 与 SwitchToAttackIntent 一致：按是否持有 AOE 能力选择进入动画
            string startAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_Start" : "Skill_1_Start";
            GD.Print($"[Brother] AfterAddedToRoom: executing TriggerAnim '{startAnim}', creatureNode={(NCombatRoom.Instance?.GetCreatureNode(Creature) != null ? "exists" : "null")}");
            await CreatureCmd.TriggerAnim(Creature, startAnim, 0.3f);
        }
        else
        {
            // 宠物不执行 RollMove（仅敌方单位会），NextMove 保持 UNSET_MOVE 导致无意图图标。
            // 显式设置强化意图，使入场时强化循环图标立即可见（SetMoveImmediate 内部会 RefreshIntents）。
            SetMoveImmediate(GetPowerUpIntent());
            DieForYou = false;
        }

        NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        // 仅本地玩家可交互：远程客户端保持游戏对远程宠物的隐藏血条/不可交互处理
        //（远程 Osty 同样不恢复交互，见 NCombatRoom.AddCreature 的通用宠物分支），
        // 避免龙舌兰 hitbox 遮挡远程玩家的出牌瞄准/点选。
        if (LocalContext.IsMe(Creature.PetOwner))
        {
            brotherNode?.ToggleIsInteractable(true);
        }
        GD.Print($"[Brother] AfterAddedToRoom: attackTurnsRemaining={attackTurnsRemaining}, IntendsToAttack={IntendsToAttack}");
    }
    // 测试牌重复打出时：回满生命值并切换为攻击循环意图
    public async Task OrderBrother()
    {
        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        await SwitchToAttackIntent();
    }

    // 回合开始（由 BrotherSupportPower 驱动）：
    // 强化循环期间获得力量。力量值的同步与达到阈值的切换由
    // BrotherPower.AfterPowerAmountChanged 实时完成（控制台加力量也能即时触发），
    // 这里无需再手动检查阈值。
    /*
    public async Task PlayerTurnStart()
    {
        if (IntendsToAttack)
        {
            return; // 攻击循环期间回合开始无动作
        }

        await PowerCmd.Apply<TraitPower>(new ThrowingPlayerChoiceContext(), Creature, TraitPerTurn, Creature, null);
        GD.Print($"[Brother] Power-up turn start: trait {Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}");
    }
    */
    public async Task PlayerTurnStart()
    {
        if (IntendsToAttack)
        {
            return; // 攻击循环期间回合开始无动作
        }

        int traitGain = Creature.HasPower<BrotherAoePower>()
            ? BrotherAoePower.TraitPerTurn
            : TraitPerTurn;

        await PowerCmd.Apply<TraitPower>(
            new ThrowingPlayerChoiceContext(),
            Creature,
            traitGain,
            Creature,
            null
        );

        GD.Print($"[Brother] Power-up turn start: trait {Creature.GetPowerAmount<TraitPower>()}/{TraitThreshold}");
    }

    // 回合结束（由 BrotherSupportPower 驱动）：
    // 攻击循环期间执行群体攻击，并结算剩余攻击回合
    /*
    public async Task TakeTurn(PlayerChoiceContext choiceContext)
    {
        if (!IntendsToAttack)
        {
            return; // 强化循环期间回合结束无动作
        }

        int hits = ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>();
        decimal damage = BasicDamage;
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0.3f);
        IReadOnlyList<Creature> targets = Creature.CombatState.GetOpponentsOf(Creature);
        // 消耗一层剑走偏锋执行攻击
        await PowerCmd.Decrement(Creature.GetPower<BrotherAttackTurnsPower>());
        for (int i = 0; i < hits; i++)
        {
            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, Creature);
        }
        if (Creature.GetPowerAmount<BrotherAttackTurnsPower>() <= 0)
        {
            // 攻击循环结束，清空特性并切回强化循环
            await PowerCmd.Remove<TraitPower>(Creature);
            await SwitchToPowerUpIntent();
        }
    }
    */
    public async Task TakeTurn(PlayerChoiceContext choiceContext)
    {
        if (!IntendsToAttack)
        {
            return; // 强化循环期间回合结束无动作
        }

        int hits = ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>();
        decimal damage = BasicDamage;

        string attackAnim = Creature.HasPower<BrotherAoePower>() ? "Skill_2_Loop" : "Attack";
        await CreatureCmd.TriggerAnim(Creature, attackAnim, 0.3f);

        // 消耗一攻击回合执行攻击
        await PowerCmd.Decrement(Creature.GetPower<BrotherAttackTurnsPower>());

        for (int i = 0; i < hits; i++)
        {
            // 每次攻击前重新获取当前存活的敌方单位
            IReadOnlyList<Creature> aliveOpponents = Creature.CombatState
                .GetOpponentsOf(Creature)
                .Where(c => c.IsAlive)
                .ToList();

            // 没有存活敌人时，剩余段数不再空挥
            if (aliveOpponents.Count == 0)
            {
                break;
            }

            IReadOnlyList<Creature> targets;
            if (Creature.HasPower<BrotherAoePower>())
            {
                // 群体攻击：对所有当前存活敌人造成伤害
                targets = aliveOpponents;
            }
            else
            {
                // 单体攻击：只攻击最左侧的存活敌人
                targets = new[] { aliveOpponents[0] };
            }

            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, Creature);
        }
        // 攻击完成后，消耗临时额外攻击段数
        if (Creature.HasPower<TemporaryExtraHitsPower>())
        {
            TemporaryExtraHitsPower tempPower = Creature.GetPower<TemporaryExtraHitsPower>();
            if (tempPower != null)
            {
                await tempPower.ConsumeAfterAttack(choiceContext);
            }
        }
        if (Creature.GetPowerAmount<BrotherAttackTurnsPower>() <= 0)
        {
            // 攻击循环结束，清空特性并切回强化循环
            await PowerCmd.Remove<TraitPower>(Creature);
            await SwitchToPowerUpIntent();
        }
    }
    // 切换为攻击循环意图
    public async Task SwitchToAttackIntent()
    {
        if (IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetAttackIntent());
        //await CreatureCmd.TriggerAnim(Creature, "Skill_1_Start", 0.3f);
        // 根据是否持有群体攻击能力选择不同动画
        string startAnim = Creature.HasPower<BrotherAoePower>()
            ? "Skill_2_Start"
            : "Skill_1_Start";
        GD.Print($"[Brother] SwitchToAttackIntent: IntendsToAttack={IntendsToAttack}, TriggerAnim '{startAnim}'");
        await CreatureCmd.TriggerAnim(Creature, startAnim, 0.3f);
        DieForYou = true;

        // 双重身份：进入攻击循环后刷新冷却
        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            doubleIdentity.RefreshCooldown();
        }
    }

    // 切换为强化循环意图
    public async Task SwitchToPowerUpIntent()
    {
        if (!IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetPowerUpIntent());
        //await CreatureCmd.TriggerAnim(Creature, "Skill_1_End", 0.3f);
        
        // 根据是否持有群体攻击能力选择不同动画
        string endAnim = Creature.HasPower<BrotherAoePower>()
            ? "Skill_2_End"
            : "Skill_1_End";
        await CreatureCmd.TriggerAnim(Creature, endAnim, 0.3f);
        DieForYou = false;

        // 双重身份：进入强化循环时触发一次
        if (Creature.GetPower<DoubleIdentityPower>() is DoubleIdentityPower doubleIdentity)
        {
            await doubleIdentity.TriggerOnPowerUpCycle();
        }
    }

    /*
    public async Task TriggerWhenGainTrait()
    {
        if (Creature.HasPower<BrotherAttackTurnsPower>())
        {
            // 已经处于攻击意图
            return;
        }
        if (Creature.GetPowerAmount<TraitPower>() >= TraitThreshold)
        {
            // 给予攻击意图
            await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, ATTACK_INTENT_TURNS, Creature, null);
        }
    }
    */
    public async Task TriggerWhenGainTrait()
    {
        if (Creature.HasPower<BrotherAttackTurnsPower>())
        {
            return;
        }

        if (Creature.GetPowerAmount<TraitPower>() >= TraitThreshold)
        {
            int attackTurns = Creature.HasPower<BrotherAoePower>()
                ? BrotherAoePower.AttackIntentTurns
                : ATTACK_INTENT_TURNS;

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
                Creature.HasPower<BrotherAoePower>()
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
