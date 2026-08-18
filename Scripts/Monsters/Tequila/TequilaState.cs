using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Pluma.Scripts.Monsters;

public class TequilaState
{
    public int Hp { get; set; }

    public int MaxHp { get; set; }

    public TequilaState()
    {
        Hp = Tequila.INITIAL_HP;
        MaxHp = Tequila.INITIAL_HP;
    }

    public static TequilaState Inst(Player player)
    {
        return Entry.TequilaStateData.Get(player);
    }

    public static int GetHp(Player player)
    {
        return Inst(player).Hp;
    }

    public static int GetMaxHp(Player player)
    {
        return Inst(player).MaxHp;
    }

    public static void SetHp(Player player, int hp)
    {
        Entry.TequilaStateData.Modify(player, state => state.Hp = Math.Max(1, hp));
    }

    public static void SetHp(Player player, int hp, int maxHp)
    {
        Entry.TequilaStateData.Modify(player, state => {
            state.Hp = Math.Max(1, hp);
            state.MaxHp = Math.Max(1, maxHp);
        });
    }

    public static void Heal(Player player, int amount)
    {
        GD.Print($"Healing Tequila for {amount} HP");
        Entry.TequilaStateData.Modify(player, state => state.Hp = Math.Min(state.MaxHp, state.Hp + amount));
    }
}