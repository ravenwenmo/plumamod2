using MegaCrit.Sts2.Core.Localization;

namespace Pluma.Scripts
{
    // 三形态卡牌（基酒/鸡尾酒）的标记接口：
    // SpiritModeDescription 为右键切换模式后的描述；
    // GetSpiritDescriptionFor 为瞄准预览时按目标阵营返回的分支描述。
    public interface ISpiritModeCard
    {
        LocString SpiritModeDescription { get; }

        LocString GetSpiritDescriptionFor(SpiritTargetBranch branch);
    }
}
