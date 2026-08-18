
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace Pluma.Scripts;

[RegisterOwnedCardKeyword(nameof(MuscleMemory), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]

[RegisterOwnedCardKeyword(nameof(Slashing), IconPath = "res://icon.svg", CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]

[RegisterOwnedCardKeyword(nameof(BaseSpirit), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)] // 新增基酒

[RegisterOwnedCardKeyword(nameof(Cocktail), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)] // 新增鸡尾酒

[RegisterOwnedCardKeyword(nameof(Brother), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
// [RegisterOwnedCardKeyword(nameof(Unique2), IconPath = "res://icon.svg")] // 如果要加更多关键词，添加特性
// 由于写法和ritsulib标准不同，这里不能用static静态类！！
public class MyKeywords
{
    public static readonly CardKeyword MuscleMemory = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(MuscleMemory)).GetModCardKeyword();
    // public static readonly CardKeyword Unique2 = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Unique2)).GetModCardKeyword();
    // 新增：切割关键词
    public static readonly CardKeyword Slashing = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Slashing)).GetModCardKeyword();
    
    public static readonly CardKeyword BaseSpirit = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(BaseSpirit)).GetModCardKeyword(); // 新增

    public static readonly CardKeyword Cocktail = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Cocktail)).GetModCardKeyword(); // 新增

    public static readonly CardKeyword Brother = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Brother)).GetModCardKeyword(); // 新增
}