
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Pluma.Scripts.Monsters;
using static Godot.Control;

namespace Pluma.Scripts.Commands;

public static class BrotherCmd
{
    /// <summary>
	/// 生成一只固定40血的龙舌兰在面前，若龙舌兰已存在，令龙舌兰回满生命值且切换为攻击循环意图。
	/// 召唤成功后写入持久化标记 HasBeenSummoned=true，
	/// 之后每场战斗开始时龙舌兰都会自动出现（见 BrotherAutoSummonSingleton）。
	/// </summary>
	/// <param name="choiceContext">玩家的choice context</param>
	/// <param name="summoner">生成龙舌兰的玩家</param>
	/// <param name="source">生成龙舌兰的 model</param>
	/// <returns>The result of the summon.</returns>
    public static async Task<SummonResult> Summon(PlayerChoiceContext choiceContext, Player summoner, AbstractModel? source)
    {
        ICombatState combatState = summoner.Creature.CombatState;
        Creature brother = combatState.Allies.FirstOrDefault((Creature c) => c.Monster is Brother && c.PetOwner == summoner);
        if (summoner.IsBrotherAlive())
        {
            // 龙舌兰已存在，令龙舌兰回满生命值并切换为攻击循环意图
            (brother.Monster as Brother)?.OrderBrother();
            return new SummonResult(brother, 0m);
        }
        else
        {
            // 龙舌兰不存在，生成龙舌兰
            brother = await PlayerCmd.AddPet<Brother>(summoner);
            NCreature brotherNode = NCombatRoom.Instance?.GetCreatureNode(brother);
            // 卡牌召唤（如测试卡 TestBrother）：此时战斗布局已完成，
            // 直接做入场滑入动画，与测试卡路径一致。
            if (brotherNode != null)
            {
                NCreature ownerNode = NCombatRoom.Instance?.GetCreatureNode(brother.PetOwner.Creature);
                if (ownerNode != null)
                {
                    Tween tween = brotherNode.CreateTween().SetParallel();
                    tween.TweenProperty(brotherNode, "position", brotherNode.Position + GetBrotherOffsetFromPlayer(brother), 0.3);
                    brotherNode.Hitbox.MouseFilter = MouseFilterEnum.Stop;
                }
            }
            await brother.Monster.AfterAddedToRoom();
            await PowerCmd.Apply<BrotherSupportPower>(new ThrowingPlayerChoiceContext(), summoner.Creature, 1m, null, null);
            brotherNode?.TrackBlockStatus(summoner.Creature);
            //额外增加一个被动
            await PowerCmd.Apply<MarkerRecoveryPower>(
                choiceContext,
                summoner.Creature,
                1m,
                null,
                null
            );

            return new SummonResult(brother, brother.CurrentHp);
        }
    }

    /// <summary>
    /// 战斗初始化阶段自动召唤龙舌兰（无卡牌来源）。
    /// 龙舌兰按持久化状态恢复力量、意图、剩余攻击回合与生命值；
    /// 若上一场战斗中死亡或从未被召唤，则按默认状态召唤。
    /// </summary>
    /// <param name="summoner">生成龙舌兰的玩家</param>
    public static async Task AutoSummon(Player summoner)
    {
        await Summon(new ThrowingPlayerChoiceContext(), summoner, null);
    }

    private static Vector2 GetBrotherOffsetFromPlayer(Creature brother)
    {
        NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(brother.PetOwner.Creature);
        if (nCreature == null)
        {
            // 节点未就绪时兜底为固定偏移，避免空引用
            return Brother.MinOffset.Lerp(Brother.MaxOffset, 1f);
        }
        return Vector2.Right * nCreature.Hitbox.Size.X * 0.5f + Brother.MinOffset.Lerp(Brother.MaxOffset, 1f);
    }
}