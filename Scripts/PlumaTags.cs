using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Pluma.Scripts;

[RegisterOwnedCardTag(nameof(Slashing))]
public class PlumaTags
{
    public static readonly CardTag Slashing = ModContentRegistry
        .GetQualifiedCardTagId(Entry.ModId, nameof(Slashing))
        .GetModCardTag();
}