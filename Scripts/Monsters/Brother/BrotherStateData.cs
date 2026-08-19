using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Utils;

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
    public static readonly SavedAttachedState<BrotherRelic, int> SavedHp = new("SavedHp", _ => Brother.INITIAL_HP);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedMaxHp = new("SavedMaxHp", _ => Brother.INITIAL_HP);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedStrength = new("SavedStrength", _ => 0);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedAttackTurnsRemaining = new("SavedAttackTurnsRemaining", _ => 0);
    public int Hp { get; set; }

    public int MaxHp { get; set; }

    // 强化循环期间积累的力量
    public int Strength { get; set; }

    // 攻击循环意图剩余回合数
    public int AttackTurnsRemaining { get; set; }

    public BrotherStateData()
    {
        Hp = Brother.INITIAL_HP;
        MaxHp = Brother.INITIAL_HP;
        Strength = 0;
        AttackTurnsRemaining = 0;
    }

    public static int GetHp(Player player)
    {
        BrotherRelic relic = player.GetRelic<BrotherRelic>();
        return SavedHp[relic];
    }

    public static int GetMaxHp(Player player)
    {
        BrotherRelic relic = player.GetRelic<BrotherRelic>();
        return SavedMaxHp[relic];
    }

    public static int GetStrength(Player player)
    {
        BrotherRelic relic = player.GetRelic<BrotherRelic>();
        return SavedStrength[relic];
    }

    public static int GetAttackTurnsRemaining(Player player)
    {
        BrotherRelic relic = player.GetRelic<BrotherRelic>();
        return SavedAttackTurnsRemaining[relic];
    }

    public static void SetFromBrother(Player player, Creature bro)
    {
        BrotherRelic.UpdateSavedHP(player, bro.CurrentHp);
        BrotherRelic.UpdateSavedMaxHP(player, bro.MaxHp);
        BrotherRelic.UpdateSavedStrength(player, bro.GetPowerAmount<StrengthPower>());
        BrotherRelic.UpdateSavedAttackTurnsRemaining(player, bro.GetPowerAmount<BrotherAttackTurnsPower>());
    }

    public static void SetHp(Player player, int hp)
    {
        BrotherRelic.UpdateSavedHP(player, hp);
    }

    public static void SetHp(Player player, int hp, int maxHp)
    {
        BrotherRelic.UpdateSavedHP(player, hp);
        BrotherRelic.UpdateSavedMaxHP(player, maxHp);
    }

    public static void SetStrength(Player player, int strength)
    {
        BrotherRelic.UpdateSavedStrength(player, strength);
    }

    public static void SetAttackTurnsRemaining(Player player, int turns)
    {
        BrotherRelic.UpdateSavedAttackTurnsRemaining(player, turns);
    }

    public static void Heal(Player player, int amount)
    {
        GD.Print($"Healing Brother for {amount} HP");
        BrotherRelic.UpdateSavedHP(player, Math.Min(BrotherStateData.GetHp(player) + amount, BrotherStateData.GetMaxHp(player)));
    }

    public static void SetDead(Player player)
    {
        BrotherRelic.UpdateSavedHP(player, 1);
        BrotherRelic.UpdateSavedStrength(player, 0);
        BrotherRelic.UpdateSavedAttackTurnsRemaining(player, 0);
    }
}