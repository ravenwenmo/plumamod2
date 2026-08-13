using MegaCrit.Sts2.Core.Localization;

namespace Pluma.Scripts
{
    // 三形态卡牌（基酒/鸡尾酒）的标记接口：提供当前模式的独立描述
    public interface ISpiritModeCard
    {
        LocString SpiritModeDescription { get; }
    }
}
