using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 解放：特性达到一定层数时，进入数回合的攻击意图。
// 同时用于检测龙舌兰生命值增减并在BrotherStateData中更新；龙舌兰死亡时重置持久化状态
[RegisterPower]
public class BrotherPower : ModPowerTemplate
{
    // 从能力列表 UI 隐藏：本能力为纯后台逻辑（代伤、状态同步、意图切换），
    // 对外展示整合进 TraitPower（特性）的描述中。隐藏不影响任何逻辑（官方机制，见 PowerModel.IsVisibleInternal）。
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    private static int TraitThreshold => Brother.TraitThreshold;
    public static int AttackIntentTurns { get; set; } = Brother.ATTACK_INTENT_TURNS;

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("TraitThreshold", TraitThreshold),
        new DynamicVar("AttackIntentTurns", AttackIntentTurns)
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherPower.png",
        BigIconPath: "res://pluma/images/powers/BrotherPower.png"
    );

    // 未格挡伤害的目标重定向 —— 双向代抗都写在这一个 override 里，绝不能拆成两个能力/两个 override：
    // Hook.ModifyUnblockedDamageTarget（src/Core/Hooks/Hook.cs:2057）只遍历一次监听器并链式传参，
    // 两个反方向的监听器会把对方的结果改回去（谁赢取决于监听器顺序，等于随机），
    // 写在同处时进入的 target 必定是原目标（玩家或龙舌兰），不存在"已被改过"的歧义。
    // 也不会有循环/连锁：CreatureCmd.Damage 每个目标只调一次本钩子，重定向后的溢出伤害
    // （OverkillDamage）由 CreatureCmd 直接打回原目标、不会再跑一遍本钩子，
    // 所以"龙舌兰替羽毛笔抗死 → 羽毛笔再替龙舌兰抗"在框架层面不存在，这里无需额外防递归。
    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal unblockedDamage, ValueProp props, Creature? _)
    {
        Creature? player = Owner.PetOwner?.Creature;
        if (player == null)
        {
            return target;
        }

        if (Owner.Monster is not Brother brother)
        {
            return target;
        }

        // 只有"有来源的攻击"才触发代抗（中毒、失去生命等不受影响）
        if (!props.IsPoweredAttack())
        {
            return target;
        }

        // 方向一：羽毛笔即将受到致命伤害，或龙舌兰正处于攻击意图 → 龙舌兰代抗。
        if (target == player)
        {
            bool lethalForPlayer = unblockedDamage >= player.CurrentHp;

            if (!brother.DieForYou && !lethalForPlayer)
            {
                return target;
            }

            if (Owner.IsDead)
            {
                return target;
            }

            // 自保：这一下会击倒龙舌兰，但羽毛笔自己扛得住 → 不许白白牺牲龙舌兰。
            // （旧逻辑在攻击意图下会照单全收，实战中就是"羽毛笔满血、龙舌兰 8 血硬吃一发 8 点、
            //  当场阵亡，羽毛笔只吃到溢出的 0~1 点"。日志里表现为 OIL_SPRAY_MOVE 之后紧跟
            //  pluma_monster_brother_die 音效、且没有任何 HP 变化打印——因为致命那一击是单段伤害。）
            if (unblockedDamage >= Owner.CurrentHp && !lethalForPlayer)
            {
                GD.Print($"[BrotherPower] 放弃代抗（会击倒龙舌兰而羽毛笔扛得住）: 伤害 {unblockedDamage}, 龙舌兰 HP {Owner.CurrentHp}, 羽毛笔 HP {player.CurrentHp}");
                return target;
            }

            GD.Print($"[BrotherPower] 龙舌兰代抗: 伤害 {unblockedDamage}, 龙舌兰 HP {Owner.CurrentHp}, 羽毛笔 HP {player.CurrentHp}");
            return Owner;
        }

        // 方向二（新增）：龙舌兰处于攻击意图时，这一下会打死它 → 羽毛笔代抗。
        if (target == Owner)
        {
            if (!brother.DieForYou)
            {
                return target;
            }

            if (Owner.IsDead || !player.IsAlive)
            {
                return target;
            }

            // 只有致命伤才接：扛得住的伤害仍由龙舌兰自己吃
            if (unblockedDamage < Owner.CurrentHp)
            {
                return target;
            }

            // 羽毛笔自己也会被这一下打死（伤害 >= 当前生命）就放弃代抗，
            // 让龙舌兰走正常流程 —— 避免"两边都接不下"时互推或白送一条命。
            if (unblockedDamage >= player.CurrentHp)
            {
                return target;
            }

            GD.Print($"[BrotherPower] 羽毛笔代抗致命伤害: 伤害 {unblockedDamage}, 龙舌兰 HP {Owner.CurrentHp}, 羽毛笔 HP {player.CurrentHp}");
            return player;
        }

        return target;
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner) return;

        if (!creature.IsAlive)
        {
            BrotherStateData.SetDead(creature.PetOwner);
            return;
        }

        BrotherStateData.SetFromBrother(creature.PetOwner, creature);

        GD.Print($"[BrotherPower] BrotherStateDataChanged: Hp: {BrotherStateData.GetHp(creature.PetOwner)}, MaxHp: {BrotherStateData.GetMaxHp(creature.PetOwner)}");
    }

    // 战斗结束时再次更新，确保保存的状态是最新的
    public override async Task AfterCombatVictory(CombatRoom combatRoom)
    {
        // 存档之前先补收尾：攻击循环的最后一击恰好斩杀最后一个敌人时，战斗当场结束，
        // 敌方回合不会再来，Brother.FinishPendingAttackCycleEnd 永远不会被调用。
        // 不收尾就会把"特性 200 + 剩余回合 0"存进下一场战斗，那边一开场又会重开一轮攻击循环。
        if (Owner.Monster is Brother brother)
        {
            await brother.FinalizeFinishedAttackCycleAtCombatEnd();
        }

        BrotherStateData.SetFromBrother(Owner.PetOwner, Owner);
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is BrotherAttackTurnsPower)
        {
            BrotherStateData.SetAttackTurnsRemaining(Owner.PetOwner, Owner.GetPowerAmount<BrotherAttackTurnsPower>());
            return;
        }

        // 只关心龙舌兰自身的特性变化
        if (power is not TraitPower || power.Owner != Owner) return;
        if (Owner.Monster is Brother brother)
        {
            BrotherStateData.SetTrait(Owner.PetOwner, Owner.GetPowerAmount<TraitPower>());
            await brother.TriggerWhenGainTrait();
        }
        else
        {
            throw new Exception("BrotherPower: Owner is not Tequila.");
        }
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (creature == Owner.PetOwner?.Creature)
        {
            await CreatureCmd.Kill(Owner, true);
        }
    }
}