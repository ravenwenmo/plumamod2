
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
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
    public static Vector2 BrotherPostion {set; get;} = Vector2.Zero;

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
            if (brotherNode != null)
            {
                // 参考 OstyCmd.Summon：卡牌召唤时节点级淡入。
                // 用节点级 Modulate，不会覆盖 Visuals 上的后排压暗（两者相乘）。
                // Brother 的 spine 没有 revive 动画，故不调 StartReviveAnim。
                if (source is CardModel)
                {
                    brotherNode.Modulate = Colors.Transparent;
                    Tween fadeTween = brotherNode.CreateTween();
                    fadeTween.TweenProperty(brotherNode, "modulate", Colors.White, 0.35).SetDelay(0.1);
                }
                // 位置滑入与交互仅本地客户端处理（参考 NCreature.OstyScaleToSize 的 IsMe 门控）：
                // 远程客户端保持游戏对通用宠物的布局与 ToggleIsInteractable(false)，
                // 避免龙舌兰 hitbox 遮挡远程玩家的出牌瞄准/点选。
                if (LocalContext.IsMe(summoner))
                {
                    NCreature ownerNode = NCombatRoom.Instance?.GetCreatureNode(brother.PetOwner.Creature);
                    if (ownerNode != null)
                    {
                        Tween tween = brotherNode.CreateTween().SetParallel();
                        BrotherPostion = brotherNode.Position + GetBrotherOffsetFromPlayer(brother);
                        tween.TweenProperty(brotherNode, "position", BrotherPostion, 0.3);
                        brotherNode.Hitbox.MouseFilter = MouseFilterEnum.Stop;
                    }
                }
            }
            await brother.Monster.AfterAddedToRoom();
            await PowerCmd.Apply<BrotherSupportPower>(new ThrowingPlayerChoiceContext(), summoner.Creature, 1m, null, null);
            brotherNode?.TrackBlockStatus(summoner.Creature);

            return new SummonResult(brother, brother.CurrentHp);
        }
    }

    /// <summary>
    /// 战斗初始化阶段自动召唤龙舌兰（无卡牌来源）。
    /// 龙舌兰按持久化状态恢复特性、意图、剩余攻击回合与生命值；
    /// 若上一场战斗中死亡或从未被召唤，则按默认状态召唤。
    /// </summary>
    /// <param name="summoner">生成龙舌兰的玩家</param>
    public static async Task AutoSummon(Player summoner)
    {
        await Summon(new ThrowingPlayerChoiceContext(), summoner, null);
    }

    public static Vector2 GetBrotherOffsetFromPlayer(Creature brother)
    {
        NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(brother.PetOwner.Creature);

        Vector2 rightOffset = new Vector2(30f, 0f); // 额外向右移动 30 像素

        if (nCreature == null)
        {
            // 节点未就绪时兜底为固定偏移，同样加上右移量
            return Brother.MinOffset.Lerp(Brother.MaxOffset, 1f) + rightOffset;
        }

        return Vector2.Right * nCreature.Hitbox.Size.X * 0.5f
               + Brother.MinOffset.Lerp(Brother.MaxOffset, 1f)
               + rightOffset;
    }
}