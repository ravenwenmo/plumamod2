using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts;

// 基酒右键循环切换 SpiritMode（Self/Enemy/Ally）时，把当前分支"借给"其 hover tip 中
// 预览的鸡尾酒牌：
// - HoverTipFactory.FromCard<T>() 的预览牌是 CardHoverTip 从 ModelDb 卡池实例克隆出来的
//   独立可变实例，与真正在场/手牌中的鸡尾酒牌实例无关，修改它不会影响鸡尾酒牌本身；
// - 这里用 ConditionalWeakTable 记录"预览实例 → 显示分支"，预览实例被 GC 后条目自动回收；
// - SpiritModeDescriptionPatch 读取该分支，让预览牌显示对应 SpiritMode 的分支描述。
public static class SpiritModeHoverPreview
{
    private sealed class BranchBox
    {
        public required SpiritTargetBranch Branch;
    }

    private static readonly ConditionalWeakTable<CardModel, BranchBox> _branches = new();

    /// <summary>
    /// 创建一张跟随来源基酒当前 SpiritMode 的鸡尾酒预览 hover tip。
    /// 与 HoverTipFactory.FromCard&lt;T&gt;() 等价，但预览牌会显示 branch 对应的分支描述。
    /// </summary>
    public static IHoverTip FromCard<TCocktail>(SpiritTargetBranch branch)
        where TCocktail : CardModel
    {
        CardModel preview = (CardModel)ModelDb.Card<TCocktail>().MutableClone();
        _branches.AddOrUpdate(preview, new BranchBox { Branch = branch });
        return HoverTipFactory.FromCard(preview);
    }

    /// <summary>返回该预览牌应显示的分支；不是本类创建的预览牌时返回 false。</summary>
    public static bool TryGetBranch(CardModel card, out SpiritTargetBranch branch)
    {
        if (_branches.TryGetValue(card, out BranchBox? box))
        {
            branch = box.Branch;
            return true;
        }
        branch = default;
        return false;
    }
}
