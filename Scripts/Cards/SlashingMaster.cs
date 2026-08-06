using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 切割大师：1费技能牌，生成3张切割。升级后生成4张。
[RegisterCard(typeof(PlumaCardPool))]
public class SlashingMaster : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 基础生成 3 张，升级后 +1 张
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("Count", 3)
    };

    public SlashingMaster() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        if (player == null) return;

        int count = DynamicVars["Count"].IntValue;
        await Slashing.CreateInHand(player, count, base.CombatState, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Count"].UpgradeValueBy(1m); // 3 → 4
    }
}