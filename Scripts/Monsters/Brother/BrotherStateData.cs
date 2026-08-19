using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Pluma.Scripts.Monsters;

// 龙舌兰（Brother）的循环意图类型
public enum BrotherIntent
{
    // 强化循环意图：每回合开始获得力量，达到阈值后切换为攻击循环
    PowerUp,
    // 攻击循环意图：造成群体伤害，持续固定回合后清空力量并切回强化循环
    Attack,
}

// 龙舌兰跨战斗持久化状态：力量、当前意图、攻击循环剩余回合数、生命值与是否召唤过标记。
// 通过 Entry.BrotherStateData（PlayerRunSavedData）注册，随存档保存/恢复。
public class BrotherStateData
{
    public int Hp { get; set; }

    public int MaxHp { get; set; }

    // 强化循环期间积累的力量
    public int Strength { get; set; }

    // 攻击循环意图剩余回合数
    public int AttackTurnsRemaining { get; set; }

    // 本局是否召唤过龙舌兰；召唤过一次后每场战斗开始自动出现（不再检查卡组条件）。
    // 龙舌兰死亡不重置本标记（见 ResetToDefault）。
    public bool HasBeenSummoned { get; set; }

    public BrotherStateData()
    {
        Hp = Brother.INITIAL_HP;
        MaxHp = Brother.INITIAL_HP;
        Strength = 0;
        AttackTurnsRemaining = 0;
        HasBeenSummoned = false;
    }

    public static BrotherStateData Inst(Player player)
    {
        return Entry.BrotherStateData.Get(player);
    }

    public static int GetHp(Player player)
    {
        return Inst(player).Hp;
    }

    public static int GetMaxHp(Player player)
    {
        return Inst(player).MaxHp;
    }

    public static int GetStrength(Player player)
    {
        return Inst(player).Strength;
    }

    public static int GetAttackTurnsRemaining(Player player)
    {
        return Inst(player).AttackTurnsRemaining;
    }

    public static void SetFromBrother(Player player, Brother bro)
    {
        Entry.BrotherStateData.Modify(player, state => {
            state.Hp = Math.Max(1, bro.Creature.CurrentHp);
            state.MaxHp = Math.Max(1, bro.Creature.MaxHp);
            state.Strength = Math.Max(0, bro.Creature.GetPowerAmount<StrengthPower>());
            state.AttackTurnsRemaining = Math.Max(0, bro.Creature.GetPowerAmount<BrotherAttackTurnsPower>());
        });
    }

    public static void SetHp(Player player, int hp)
    {
        Entry.BrotherStateData.Modify(player, state => state.Hp = Math.Max(1, hp));
    }

    public static void SetHp(Player player, int hp, int maxHp)
    {
        Entry.BrotherStateData.Modify(player, state => {
            state.Hp = Math.Max(1, hp);
            state.MaxHp = Math.Max(1, maxHp);
        });
    }

    public static void SetStrength(Player player, int strength)
    {
        Entry.BrotherStateData.Modify(player, state => state.Strength = Math.Max(0, strength));
    }

    public static void SetAttackTurnsRemaining(Player player, int turns)
    {
        Entry.BrotherStateData.Modify(player, state => state.AttackTurnsRemaining = Math.Max(0, turns));
    }

    public static void Heal(Player player, int amount)
    {
        GD.Print($"Healing Brother for {amount} HP");
        Entry.BrotherStateData.Modify(player, state => state.Hp = Math.Min(state.MaxHp, state.Hp + amount));
    }

    public static void SetHasSummoned(Player player)
    {
        Entry.BrotherStateData.Modify(player, state => state.HasBeenSummoned = true);
    }

    public static void SetDead(Player player)
    {
        Entry.BrotherStateData.Modify(player, state => {
            state.Hp = 1;
            state.Strength = 0;
            state.AttackTurnsRemaining = 0;
        });
    }
}