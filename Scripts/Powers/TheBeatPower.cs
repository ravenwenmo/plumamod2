using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels; // 新增
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 节奏感：每打出3张本能牌，获得 1 点能量。左下角显示还需打出的本能牌数。
[RegisterPower]
public class TheBeatPower : ModPowerTemplate, IPowerExtraIconAmountLabelSpecsProvider
{
    // 本能牌计数。internal：CombatSolver 适配层需要直接读写预测克隆上的该字段
    // （普通私有字段会随 MemberwiseClone 带入预测分支，根捕获时即携带实机进度）。
    internal int MuscleMemoryCount;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TheBeatPower.png",
        BigIconPath: "res://pluma/images/powers/TheBeatPower.png"
    );

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == base.Owner &&
            cardPlay.Card.Keywords.Contains(MyKeywords.MuscleMemory))
        {
            MuscleMemoryCount++;

            if (MuscleMemoryCount >= 3)
            {
                await PlayerCmd.GainEnergy(1, base.Owner.Player);
                MuscleMemoryCount = 0; // 重置计数，开始下一轮
            }

            InvokeDisplayAmountChanged();
        }
    }

    // 角标显示：左下角显示还需打出的本能牌数（3 - 当前计数）
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetPowerExtraIconAmountLabelSpecs()
    {
        int remaining = 3 - MuscleMemoryCount;
        if (remaining < 0) remaining = 0;

        return
        [
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomLeft,
                $"还需[color=gold]{remaining}[/color]张"
            )
        ];
    }
}