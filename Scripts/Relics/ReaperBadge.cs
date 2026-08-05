using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Pluma.Scripts;

// 注册遗物。如果要写自定义池看添加人物的开头
[RegisterRelic(typeof(PlumaRelicPool))]
[RegisterCharacterStarterRelic(typeof(PlumaCharacter))] // 注册起始遗物
public class ReaperBadge : ModRelicTemplate
{
    // 稀有度
    public override RelicRarity Rarity => RelicRarity.Starter;

    // 遗物的数值。这里会替换本地化中的{Cards}。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => new(
        // 小图标（原版85x85）
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        // 轮廓图标（原版85x85）
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        // 大图标（原版256x256）
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            Flash();
            await PowerCmd.Apply<ReaperHealingPower>(new ThrowingPlayerChoiceContext(), base.Owner.Creature, 1, base.Owner.Creature, null);
        }
    }
}