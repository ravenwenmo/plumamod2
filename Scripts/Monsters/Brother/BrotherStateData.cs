using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Utils;

using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Utils;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Runs;

namespace Pluma.Scripts.Monsters;

// 龙舌兰（Brother）的循环意图类型
public enum BrotherIntent
{
    // 强化循环意图：每回合开始获得特性，达到阈值后切换为攻击循环
    PowerUp,
    // 攻击循环意图：造成群体伤害，持续固定回合后清空特性并切回强化循环
    Attack,
}

// 龙舌兰跨战斗持久化状态：特性、当前意图、攻击循环剩余回合数、生命值与是否召唤过标记。
// 通过 Entry.BrotherStateData（PlayerRunSavedData）注册，随存档保存/恢复。
public class BrotherStateData
{
    public static readonly SavedAttachedState<BrotherRelic, int> SavedHp = new("SavedHp", _ =>
    {
        if (RunManager.Instance.HasAscension(AscensionLevel.WearyTraveler)) {
            return (int)(Brother.INITIAL_HP * 0.8);
        } else
        {
            return Brother.INITIAL_HP;
        }
    });
    public static readonly SavedAttachedState<BrotherRelic, int> SavedMaxHp = new("SavedMaxHp", _ => Brother.INITIAL_HP);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedTrait = new("SavedTrait", _ => 0);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedAttackTurnsRemaining = new("SavedAttackTurnsRemaining", _ => 0);
    public static readonly SavedAttachedState<BrotherRelic, int> SavedPendingHeal = new("SavedPendingHeal", _ => 0);
    
    public int Hp { get; set; }
    public int MaxHp { get; set; }

    // 强化循环期间积累的特性层数
    public int Trait { get; set; }

    // 攻击循环意图剩余回合数
    public int AttackTurnsRemaining { get; set; }

    public BrotherStateData()
    {
        Hp = Brother.INITIAL_HP;
        MaxHp = Brother.INITIAL_HP;
        Trait = 0;
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

    public static int GetTrait(Player player)
    {
        BrotherRelic relic = player.GetRelic<BrotherRelic>();
        return SavedTrait[relic];
    }
    
    public static void AddPendingHeal(Player player, int amount)
    {
        int current = SavedPendingHeal[player.GetRelic<BrotherRelic>()];
        SavedPendingHeal[player.GetRelic<BrotherRelic>()] = current + amount;
    }

    public static int GetPendingHeal(Player player)
    {
        return SavedPendingHeal[player.GetRelic<BrotherRelic>()];
    }

    public static void ClearPendingHeal(Player player)
    {
        SavedPendingHeal[player.GetRelic<BrotherRelic>()] = 0;
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
        BrotherRelic.UpdateSavedTrait(player, bro.GetPowerAmount<TraitPower>());
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

    public static void SetTrait(Player player, int trait)
    {
        BrotherRelic.UpdateSavedTrait(player, trait);
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
        BrotherRelic.UpdateSavedTrait(player, 0);
        BrotherRelic.UpdateSavedAttackTurnsRemaining(player, 0);
    }
}