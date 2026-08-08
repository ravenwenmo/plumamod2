using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;

namespace Pluma.Scripts;

// 黎博利思维：商店遗物。进入战斗时，获得2层黎博利，所有敌人获得1层黎博利。
[RegisterRelic(typeof(PlumaRelicPool))]
public class LiberiLens : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom) return;

        Flash();

        // 给自己2层黎博利
        await PowerCmd.Apply<LiberiPower>(
            new ThrowingPlayerChoiceContext(),
            base.Owner.Creature,
            2,
            base.Owner.Creature,
            null
        );

        // 给所有可攻击敌人1层黎博利
        var enemies = base.Owner.Creature.CombatState.HittableEnemies;
        foreach (var enemy in enemies)
        {
            await PowerCmd.Apply<LiberiPower>(
                new ThrowingPlayerChoiceContext(),
                enemy,
                1,
                base.Owner.Creature,
                null
            );
        }
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<LiberiPower>(),
        HoverTipFactory.FromPower<OpenWoundPower>(),
    };

}