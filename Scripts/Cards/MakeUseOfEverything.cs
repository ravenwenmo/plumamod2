using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars; // 提供 ModCardVars
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 物尽其用：1费能力牌。每当打出一张辅料组合包时，额外获得4点格挡。升级后额外获得6点。
[RegisterCard(typeof(PlumaCardPool))]
public class MakeUseOfEverything : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Power;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 动态变量：额外格挡值，基础4，升级后6
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("BlockAmount", 3)
    };

    // 悬浮提示：显示辅料组合包预览
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromCard<MixerPack>()
    };

    public MakeUseOfEverything() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int blockAmount = DynamicVars["BlockAmount"].IntValue;
        await PowerCmd.Apply<MakeUseOfEverythingPower>(
            choiceContext,
            base.Owner.Creature,
            blockAmount,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        // 额外格挡从4提升到6
        DynamicVars["BlockAmount"].UpgradeValueBy(2m);
    }
}