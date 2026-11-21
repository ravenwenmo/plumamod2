using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using Pluma.Scripts.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace Pluma.Scripts.Vfx;

/// <summary>
/// 每次进入战斗房间时，在部署动画播放的同时给玩家干员放一次官方入场特效（crow_start_01）。
/// 只对玩家生物（含联机队友）生效，怪物不处理。
/// </summary>
[RegisterSingleton]
public class DeployVfxHook : HookedSingletonModel
{
    public DeployVfxHook() : base(HookType.Combat)
    {
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom) return;

        // 部署动画起步后小人节点才建好，轮询等它出现（最多约 2s）
        for (int i = 0; i < 20; i++)
        {
            await Cmd.CustomScaledWait(0.05f, 0.2f);

            NCombatRoom? combat = NCombatRoom.Instance;
            if (combat == null) continue;

            bool played = false;
            foreach (NCreature node in combat.CreatureNodes)
            {
                if (node.Entity is not { IsPlayer: true } creature) continue;
                OfficialFx.PlayDeploy(creature);
                played = true;
            }

            if (played) return;
        }
    }
}
