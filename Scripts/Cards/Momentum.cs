using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.Creatures;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts;
//势头,获得力量
[RegisterCard(typeof(PlumaCardPool))]
public class Momentum : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Power;         // 能力牌
    private const CardRarity rarity = CardRarity.Rare;  
    private const bool shouldShowInCardLibrary = true;
    // 目标类型（AnyEnemy表示任意敌人）
    private const TargetType targetType = TargetType.Self;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 添加自定义关键词“本能”
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        MyKeywords.MuscleMemory
    ];

    // 用 DynamicVar 管理层数（基础2，升级+1变为3）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        ModCardVars.Int("StrengthAmount", 2)
    ];

    public Momentum() :   base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 对自己施加力量
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["StrengthAmount"].BaseValue,
            base.Owner.Creature,
            this
        );
        Creature brother = base.Owner.Brother();
        Creature target = (brother != null && brother.IsAlive) ? brother : base.Owner.Creature;

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            target,
            DynamicVars["StrengthAmount"].BaseValue,
            base.Owner.Creature,
            this
        );
        
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<StrengthPower>()
    };
    
    protected override void OnUpgrade()
    {
        // 升级后力量从2变为3
        //DynamicVars["StrengthAmount"].UpgradeValueBy(1m);
        base.EnergyCost.UpgradeBy(-1); // 2费 → 1费
    }
}