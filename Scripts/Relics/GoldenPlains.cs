using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 黄金平原：每洗牌两次，获得1点能量。
[RegisterRelic(typeof(PlumaRelicPool))]
public class GoldenPlains : ModRelicTemplate
{
    private int _shuffleCount;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new EnergyVar(1) };

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
            _shuffleCount = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (shuffler != base.Owner) return;

        _shuffleCount++;
        if (_shuffleCount >= 2)
        {
            _shuffleCount = 0;
            Flash();
            await PlayerCmd.GainEnergy(1, base.Owner);
        }
    }
}