using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;


namespace Pluma.Scripts.Cards;

// 补货联络：1费罕见技能牌。获得1张辅料组合包，并在接下来的2个回合开始时各「随机基酒 1」。升级后持续3回合。
[RegisterCard(typeof(PlumaCardPool))]
public class RestockCall : ModCardTemplate, IBaseSpiritRelatedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 持续回合数：基础2，升级后3
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        ModCardVars.Int("Turns", 2)
    };

    // 悬浮提示：显示基酒、辅料组合包预览与「随机基酒」术语
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        BaseSpiritGeneration.RandomBaseSpiritHoverTip,
        HoverTipFactory.FromCard<MixerPack>(upgrade: base.IsUpgraded)
    };

    public RestockCall() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;

        // 1. 获得一张辅料组合包
        var mixerPack = base.CombatState.CreateCard<MixerPack>(player);
        if (base.IsUpgraded)
        {
            CardCmd.Upgrade(mixerPack);
        }
        await CardPileCmd.AddGeneratedCardsToCombat(new[] { mixerPack }, PileType.Hand, player);

        // 2. 施加补货能力，持续回合数由 Turns 变量决定
        var power = await PowerCmd.Apply<RestockCallPower>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
        if (power != null)
        {
            power.SetTurns(DynamicVars["Turns"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后持续回合数 +1：2 → 3
        DynamicVars["Turns"].UpgradeValueBy(1m);
    }
}