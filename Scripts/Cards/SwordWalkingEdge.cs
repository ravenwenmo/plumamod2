using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Pluma.Scripts.Monsters;


namespace Pluma.Scripts.Cards;

// 剑走偏锋：无论龙舌兰是否已有范围攻击，都对其施加一次范围攻击。
// 范围攻击能力会自行处理重复施加，并在龙舌兰处于蓄力状态时将蓄力层数变为3。
[RegisterCard(typeof(PlumaCardPool))]
public class SwordWalkingEdge : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        CardKeyword.Ethereal // 虚无
    };

    public SwordWalkingEdge()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature creature = base.Owner.Brother();

        if (creature == null || !creature.IsAlive || creature.Monster is not Brother brother)
        {
            return;
        }

        // 直接施加范围攻击能力，目标使用 Creature 对象
        await PowerCmd.Apply<BrotherAoePower>(
            choiceContext,
            creature,
            1m,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal); // 敲后去掉虚无
    }
}