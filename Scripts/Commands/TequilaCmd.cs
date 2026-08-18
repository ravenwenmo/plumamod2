
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Pluma.Scripts.Monsters;
using static Godot.Control;

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
        Creature tequila = combatState.Allies.FirstOrDefault((Creature c) => c.Monster is Monsters.Tequila && c.PetOwner == summoner);
        if (summoner.IsTequilaAlive())
        {
            // 龙舌兰已存在，令龙舌兰执行行动并切换意图
            (tequila.Monster as Monsters.Tequila)?.OrderTequila();
            return new SummonResult(tequila, 0m);
        }
        else
        {
            // 龙舌兰不存在，生成龙舌兰
            tequila = await PlayerCmd.AddPet<Monsters.Tequila>(summoner);
            NCreature tequilaNode = NCombatRoom.Instance?.GetCreatureNode(tequila);
            await tequila.Monster.AfterAddedToRoom();
            if (tequilaNode != null && source is CardModel)
            {
                Tween tween = tequilaNode.CreateTween().SetParallel();
                tween.TweenProperty(tequilaNode, "position", tequilaNode.Position + GetTequilaOffsetFromPlayer(tequila), 0.3);
                tequilaNode.Hitbox.MouseFilter = MouseFilterEnum.Stop;
            }
            await PowerCmd.Apply<TequilaSupportPower>(new ThrowingPlayerChoiceContext(), summoner.Creature, 1m, null, null);
            tequilaNode?.TrackBlockStatus(summoner.Creature);

            return new SummonResult(tequila, tequila.CurrentHp);
        }
    }

    private static Vector2 GetTequilaOffsetFromPlayer(Creature tequila)
    {
        NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(tequila.PetOwner.Creature);
        return Vector2.Right * nCreature.Hitbox.Size.X * 0.5f + Monsters.Tequila.MinOffset.Lerp(Monsters.Tequila.MaxOffset, 1f);
    }
}