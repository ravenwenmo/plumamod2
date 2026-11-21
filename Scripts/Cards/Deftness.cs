using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 灵巧：1费普通技能牌。本回合失去1点力量与1点敏捷，换取本回合攻击段数翻倍、
// 施加的创伤层数翻倍。升级后获得本能（不再有数值变化）。
[RegisterCard(typeof(PlumaCardPool))]
public class Deftness : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 失去的力量/敏捷点数（固定 1 点，升级不改变）
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("Stacks", 1)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<OpenWoundPower>(),
        HoverTipFactory.FromPower<DeftnessPower>(),
        HoverTipFactory.FromPower<DeftnessDownPower>()
    };

    public Deftness() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int stacks = DynamicVars["Stacks"].IntValue;
        var creature = base.Owner.Creature;

        // 本回合失去力量与敏捷（回合结束时自动返还）
        await PowerCmd.Apply<DeftnessDownPower>(choiceContext, creature, stacks, creature, this);

        // 本回合攻击段数与施加的创伤翻倍
        await PowerCmd.Apply<DeftnessPower>(choiceContext, creature, 1m, creature, this);
    }

    // 升级：获得本能关键词
    protected override void OnUpgrade()
    {
        AddKeyword(MyKeywords.MuscleMemory);
    }
}
