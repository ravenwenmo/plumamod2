using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Pluma.Scripts.Commands;
using Pluma.Scripts.Monsters;

/// <summary>
/// 多人模式位置补丁：游戏对本地 Osty 在 NCombatRoom.PositionPlayersAndPets 里有特殊摆放逻辑
///（PositionLocalPlayerOsty），但该逻辑硬编码 Necrobinder/Osty，模组宠物走不到。
/// 当战斗房间创建/布局晚于龙舌兰召唤时（多人模式下自动召唤时序不稳定），
/// BrotherCmd.Summon 里的入场 tween 会因节点未就绪被跳过；
/// 这里在布局完成后补上本地玩家龙舌兰的偏移位置与可交互状态。
/// 纯视觉层、每客户端本地执行，不写入游戏状态，无需同步。
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
public static class BrotherLayoutPatch
{
    [HarmonyPostfix]
    public static void Postfix(List<NCreature> creatureNodes)
    {
        foreach (NCreature creatureNode in creatureNodes)
        {
            if (creatureNode.Entity.Monster is not Brother)
            {
                continue;
            }
            // 远程玩家的龙舌兰保持通用宠物布局（与远程 Osty 一致），只处理本地玩家
            if (!LocalContext.IsMe(creatureNode.Entity.PetOwner))
            {
                continue;
            }
            // 与 BrotherCmd.Summon 的 tween 目标公式一致：布局位置 + 偏移
            creatureNode.Position += BrotherCmd.GetBrotherOffsetFromPlayer(creatureNode.Entity);
            creatureNode.ToggleIsInteractable(true);
        }
    }
}
