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
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 第一法则：每洗牌两次，获得2点能量。
// 主计数显示“本次战斗已洗牌次数”（0 → 1 → 归零），与本体计数遗物一致（同 GoldenPlains）。
[RegisterRelic(typeof(PlumaRelicPool))]
public class PrimaRegola : ModRelicTemplate, IRelicExtraIconAmountLabelSpecsProvider
{
    // 触发所需洗牌次数。
    public const int ShufflesPerTrigger = 2;

    // internal：CombatSolver 适配层直接读写预测克隆上的字段（同 GoldenPlains）。
    internal int ShuffleCount;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new EnergyVar(2) };

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            ShuffleCount = 0;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
    {
        if (shuffler != base.Owner) return;

        ShuffleCount++;
        if (ShuffleCount >= ShufflesPerTrigger)
        {
            ShuffleCount = 0;
            Flash();
            await PlayerCmd.GainEnergy(2, base.Owner);
        }
        InvokeDisplayAmountChanged();
    }

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 显示已洗牌次数（0/1），第 2 次洗牌时归零 —— 与本体计数遗物一致。
        int progress = ShuffleCount % ShufflesPerTrigger;
        if (progress < 0) progress = 0;

        return
        [
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomRight,   // 使用主计数位置
                $"{progress}"
            )
        ];
    }
}