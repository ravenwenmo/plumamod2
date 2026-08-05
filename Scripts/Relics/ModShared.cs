using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

[RegisterRelic(typeof(PlumaRelicPool))]
public class ModShard : ModRelicTemplate
{
    private bool _strengthApplied;

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new PowerVar<StrengthPower>(2m) };

    [SavedProperty]
    public bool StrengthApplied
    {
        get => _strengthApplied;
        set
        {
            AssertMutable();
            if (_strengthApplied != value)
            {
                _strengthApplied = value;
                base.Status = _strengthApplied ? RelicStatus.Active : RelicStatus.Normal;
            }
        }
    }

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            StrengthApplied = false;
            _ = CheckAndUpdateStrength();
        }
        return Task.CompletedTask;
    }

    // 回合开始兜底检查
    public override async Task AfterEnergyReset(Player player)
    {
        if (player != base.Owner) return;
        await CheckAndUpdateStrength();
    }

    // 供外部调用的公开方法
    public async Task CheckAndUpdateStrength()
    {
        var owner = base.Owner.Creature;
        int flowStacks = (int)owner.GetPowerAmount<FlowState>();
        bool shouldHaveStrength = flowStacks > 8;

        if (shouldHaveStrength && !StrengthApplied)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                owner,
                2,
                owner,
                null
            );
            StrengthApplied = true;
        }
        else if (!shouldHaveStrength && StrengthApplied)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(
                new ThrowingPlayerChoiceContext(),
                owner,
                -2,
                owner,
                null
            );
            StrengthApplied = false;
        }
    }
}