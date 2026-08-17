
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts.Commands;

public static class TequilaCmd
{
    /// <summary>
	/// 生成一只固定20血的龙舌兰在面前，若龙舌兰已存在，令龙舌兰回满生命值且意图变为攻击
	/// </summary>
	/// <param name="choiceContext">玩家的choice context</param>
	/// <param name="summoner">生成龙舌兰的玩家</param>
	/// <param name="source">生成龙舌兰的 model</param>
	/// <returns>The result of the summon.</returns>
    public static async Task<SummonResult> Summon(PlayerChoiceContext choiceContext, Player summoner, AbstractModel? source)
    {
        ICombatState combatState = summoner.Creature.CombatState;
        if (summoner.IsTequilaAlive())
        {
            // 龙舌兰已存在，令龙舌兰回满生命值且意图变为攻击
            var tequila = summoner.Tequila()!;
            await CreatureCmd.Heal(tequila, tequila.MaxHp);
            (tequila.Monster as Monsters.Tequila)?.SwitchToAttackIntent();
            return new SummonResult(tequila, 0m);
        }
        else
        {
            // 龙舌兰不存在，生成龙舌兰
            Creature tequila = await PlayerCmd.AddPet<Monsters.Tequila>(summoner);
            NCreature tequilaNode = NCombatRoom.Instance?.GetCreatureNode(tequila);
            if (tequilaNode != null && source is CardModel)
            {
                tequilaNode.Modulate = Colors.Transparent;
                Tween tween = tequilaNode.CreateTween();
                tween.TweenProperty(tequilaNode, "modulate", Colors.White, 0.3499999940395355).SetDelay(0.10000000149011612);
                tequilaNode.StartSummonAnim();
            } 
            tequilaNode?.TrackBlockStatus(summoner.Creature);
            

            int hp = Monsters.Tequila.INITIAL_HP;
            CombatManager.Instance.History.Summoned(combatState, hp, summoner);
            await Hook.AfterSummon(combatState, choiceContext, summoner, hp);
            return new SummonResult(summoner.Osty, hp);
        }
    }
}