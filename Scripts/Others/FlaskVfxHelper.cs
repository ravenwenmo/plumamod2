using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions; // 提供 PoisonPotion


namespace Pluma.Scripts;

public static class FlaskVfxHelper
{
    // 播放一次投掷 + 击中动画。未传纹理时默认使用原版毒药瓶图像。
    public static async Task PlaySimpleThrow(Creature source, Creature target, Texture2D? flaskTexture = null)
    {
        if (TestMode.IsOn) return;
        if (source == null || target == null) return;

        var sourceNode = NCombatRoom.Instance.GetCreatureNode(source);
        var targetNode = NCombatRoom.Instance.GetCreatureNode(target);
        if (sourceNode == null || targetNode == null) return;

        // 默认纹理：原版毒药瓶
        Texture2D texture = flaskTexture ?? ModelDb.Potion<PoisonPotion>().Image;

        Vector2 startPos = sourceNode.VfxSpawnPosition;
        Vector2 endPos = targetNode.GetBottomOfHitbox();

        var throwVfx = NItemThrowVfx.Create(startPos, endPos, texture);
        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(throwVfx);
        await Cmd.Wait(0.4f);

        var splash = NSplashVfx.Create(targetNode.VfxSpawnPosition, new Color("83eb85"));
        NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(splash);
    }
}