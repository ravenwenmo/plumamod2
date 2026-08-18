using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
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
    public const int INITIAL_HP = 40;
    // 强化循环意图每回合获得的力量
    private const int PowerUpStrengthPerTurn = 1;
    // 力量达到该值后切换为攻击循环意图
    public const int STRENGTH_THRESHOLD = 8;
    // 攻击循环意图持续的回合数（变量，可调整）
    public const int ATTACK_INTENT_TURNS = 3;
    // 攻击基础段数（额外段数由 BrotherExtraHitsPower 层数提供）
    public const int ATTACK_BASE_HITS = 1;
    // 每段基础伤害
    private const int BasicDamage = 4;

    public override int MinInitialHp => INITIAL_HP;
    public override int MaxInitialHp => INITIAL_HP;
    public static Vector2 MinOffset => new Vector2(10f, -10f);
	public static Vector2 MaxOffset => new Vector2(50f, -10f);
    public override bool IsHealthBarVisible => true;

    // 召唤龙舌兰的玩家（AfterAddedToRoom 中赋值）。
    // 攻击的伤害来源为龙舌兰自身，宠物 dealer 由 PersonalHivePower_NullDealerCheckPatch 精准拦截
    public Player? Summoner { get; set; }

    // 攻击循环意图期间为玩家吸收未格挡的攻击伤害（见 BrotherPower / DamageBlockInternalPatch）
    public bool DieForYou { get; set; } = false;

    // 怪物场景
    public override MonsterAssetProfile AssetProfile => new(
        VisualsScenePath: "res://pluma/images/spineAni/bro/Bro.tscn"
    );

    // 自动转换怪物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.VisualsScenePath!);

    // 当前持久化状态
    private BrotherStateData State => Entry.BrotherStateData.Get(Creature.PetOwner);

    public override async Task AfterAddedToRoom()
    {
        Summoner = Creature.PetOwner;

        // 按持久化数据恢复生命值、力量与意图。
        // 注意顺序：先设置意图再恢复力量，避免力量恢复触发
        // BrotherPower.AfterPowerAmountChanged 时误切意图（幂等保护见 SwitchToAttackIntent）。
        BrotherStateData state = Entry.BrotherStateData.Get(Creature.PetOwner);
        await CreatureCmd.SetMaxHp(Creature, Math.Max(1, state.MaxHp));
        await CreatureCmd.SetCurrentHp(Creature, Math.Max(1, state.Hp));
        await PowerCmd.Apply<BrotherPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, null, null);

        int strengthToApply;
        if (state.Intent == BrotherIntent.Attack)
        {
            // 上一场战斗在攻击循环中结束：攻击循环不跨战斗继承，
            // 按"攻击循环结束"处理——清空力量并直接切回强化循环。
            // 强化循环的继承逻辑不变（按 state.Strength 恢复力量）。
            Entry.BrotherStateData.Modify(Creature.PetOwner, s => {
                s.Strength = 0;
                s.Intent = BrotherIntent.PowerUp;
                s.AttackTurnsRemaining = ATTACK_INTENT_TURNS;
            });
            BrotherStateData.SyncStrength(Creature.PetOwner, 0);
            strengthToApply = 0;
            SetMoveImmediate(GetPowerUpIntent());
            DieForYou = false;
            GD.Print("[Brother] Previous combat ended in attack loop, reset to power-up intent");
        }
        else
        {
            // 内存镜像优先（防存档快照覆盖高频字段），不一致时修复槽位值
            int bagStrength = state.Strength;
            strengthToApply = BrotherStateData.GetEffectiveStrength(Creature.PetOwner, bagStrength);
            if (strengthToApply != bagStrength)
            {
                Entry.BrotherStateData.Modify(Creature.PetOwner, s => s.Strength = strengthToApply);
                GD.Print($"[Brother] Bag strength was {bagStrength}, healed from mirror to {strengthToApply}");
            }
            SetMoveImmediate(GetPowerUpIntent());
            DieForYou = false;
        }

        if (strengthToApply > 0)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, strengthToApply, Creature, null);
        }

        GD.Print($"[Brother] Added to room. Hp: {Creature.CurrentHp}/{Creature.MaxHp}, Strength: {state.Strength}, Intent: {state.Intent}, AttackTurnsRemaining: {state.AttackTurnsRemaining}");

        NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(Creature);
        brotherNode?.ToggleIsInteractable(true);
    }

    // 测试牌重复打出时：回满生命值并切换为攻击循环意图
    public async Task OrderBrother()
    {
        await CreatureCmd.SetCurrentHp(Creature, Creature.MaxHp);
        Entry.BrotherStateData.Modify(Creature.PetOwner, s => s.Hp = s.MaxHp);
        await SwitchToAttackIntent();
    }

    // 回合开始（由 BrotherSupportPower 驱动）：
    // 强化循环期间获得力量。力量值的同步与达到阈值的切换由
    // BrotherPower.AfterPowerAmountChanged 实时完成（控制台加力量也能即时触发），
    // 这里无需再手动检查阈值。
    public async Task OnSideTurnStart()
    {
        if (IntendsToAttack)
        {
            return; // 攻击循环期间回合开始无动作
        }

        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, PowerUpStrengthPerTurn, Creature, null);
        GD.Print($"[Brother] Power-up turn start: strength {Creature.GetPowerAmount<StrengthPower>()}/{STRENGTH_THRESHOLD}");
    }

    // 回合结束（由 BrotherSupportPower 驱动）：
    // 攻击循环期间执行群体攻击，并结算剩余攻击回合
    public async Task OnSideTurnEnd(PlayerChoiceContext choiceContext)
    {
        if (!IntendsToAttack)
        {
            return; // 强化循环期间回合结束无动作
        }

        int hits = ATTACK_BASE_HITS + Creature.GetPowerAmount<BrotherExtraHitsPower>();
        // 每段基础伤害；龙舌兰的力量经 StrengthPower.ModifyDamageAdditive 原生计入
        //（dealer 为龙舌兰自身，即力量持有者，无需手工叠加）
        decimal damage = BasicDamage;
        // 以龙舌兰自身作为伤害来源（召唤物 dealer，非 null）：
        // 需要按 dealer.Player 生成状态牌的能力（如 PersonalHivePower）由精准补丁拦截，
        // 见 PersonalHivePower_NullDealerCheckPatch，不会出现空引用。
        Creature dealer = Creature;

        GD.Print($"[Brother] Executing attack move: hits={hits}, damage={damage}, dealer={dealer}");
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0.3f);
        IReadOnlyList<Creature> targets = Creature.CombatState.GetOpponentsOf(Creature);
        for (int i = 0; i < hits; i++)
        {
            await CreatureCmd.Damage(choiceContext, targets, damage, ValueProp.Move, dealer);
        }

        int remaining = State.AttackTurnsRemaining - 1;
        Entry.BrotherStateData.Modify(Creature.PetOwner, s => s.AttackTurnsRemaining = remaining);
        GD.Print($"[Brother] Attack turn end: turns remaining {remaining}");

        // 同步剩余攻击回合显示（先移除再按剩余值施加，保证层数与剩余回合一致）
        await PowerCmd.Remove<BrotherAttackTurnsPower>(Creature);
        if (remaining > 0)
        {
            await PowerCmd.Apply<BrotherAttackTurnsPower>(choiceContext, Creature, remaining, Creature, null);
        }

        if (remaining <= 0)
        {
            // 攻击循环结束：清空力量并切回强化循环
            await PowerCmd.Remove<StrengthPower>(Creature);
            Entry.BrotherStateData.Modify(Creature.PetOwner, s => s.Strength = 0);
            BrotherStateData.SyncStrength(Creature.PetOwner, 0);
            await SwitchToPowerUpIntent();
        }
    }

    // 切换为攻击循环意图（公开：供 BrotherPower.AfterPowerAmountChanged 实时触发时调用）。
    // 幂等保护：已在攻击循环中时直接返回，防止力量恢复/重复触发时重置剩余回合。
    public async Task SwitchToAttackIntent()
    {
        if (IntendsToAttack)
        {
            return;
        }

        SetMoveImmediate(GetAttackIntent());
        await CreatureCmd.TriggerAnim(Creature, "Skill_1_Start", 0.3f);
        DieForYou = true;
        Entry.BrotherStateData.Modify(Creature.PetOwner, s => {
            s.Intent = BrotherIntent.Attack;
            s.AttackTurnsRemaining = ATTACK_INTENT_TURNS;
        });
        // 同步显示剩余攻击回合的能力
        await PowerCmd.Apply<BrotherAttackTurnsPower>(new ThrowingPlayerChoiceContext(), Creature, ATTACK_INTENT_TURNS, Creature, null);
        GD.Print("[Brother] Switched to attack intent");
    }

    // 切换为强化循环意图
    private async Task SwitchToPowerUpIntent()
    {
        SetMoveImmediate(GetPowerUpIntent());
        await CreatureCmd.TriggerAnim(Creature, "Skill_1_End", 0.3f);
        DieForYou = false;
        // 移除剩余回合显示能力
        await PowerCmd.Remove<BrotherAttackTurnsPower>(Creature);
        Entry.BrotherStateData.Modify(Creature.PetOwner, s => {
            s.Intent = BrotherIntent.PowerUp;
            s.AttackTurnsRemaining = ATTACK_INTENT_TURNS;
        });
        GD.Print("[Brother] Switched to power-up intent");
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
            new BuffIntent()
        );

        return powerUpIntent;
    }

    private MoveState GetAttackIntent()
    {
        var attackIntent = new MoveState(
            "ATTACK",
            AttackMove,
            new SingleAttackIntent(BasicDamage)
        );

        return attackIntent;
    }

    // 意图动作实际由 OnSideTurnStart / OnSideTurnEnd 驱动，这里仅作为状态机的占位执行器
    private async Task PowerUpMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

    private async Task AttackMove(IReadOnlyList<Creature> targets)
    {
        await Task.CompletedTask;
    }

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
	}
}