using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;


namespace Pluma.Scripts;
[RegisterSingleton]
public class IaiSlashTriggerSingleton : HookedSingletonModel
{
    public static IaiSlashTriggerSingleton Instance { get; private set; }

    private bool _isAutoPlaying;

    public IaiSlashTriggerSingleton() : base(HookType.Combat)
    {
        Instance = this;
    }

    private static bool CanTrigger(Creature? target, Creature? dealer)
        => target is { IsPlayer: true } && dealer is { IsMonster: true };

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_isAutoPlaying) return;
        if (!CanTrigger(target, dealer) || dealer == null) return;
        if (target.Player == null) return;

        var hand = PileType.Hand.GetPile(target.Player);
        IaiSlash? iai = hand.Cards.OfType<IaiSlash>().FirstOrDefault();
        if (iai == null) return;

        _isAutoPlaying = true;
        try
        {
            await CardCmd.AutoPlay(choiceContext, iai, dealer);
        }
        finally
        {
            _isAutoPlaying = false;
        }
    }
}