using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;



namespace Pluma.Scripts;

// 黎博利咬：2费技能牌，保留，对一名敌人施加7层创伤。升级后施加10层。
[RegisterCard(typeof(PlumaCardPool))]
public class LiberiBite : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 保留关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    // 创伤层数变量
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("OpenWound", 7)
    };


    public LiberiBite() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // 再施加创伤层数
        await PowerCmd.Apply<OpenWoundPower>(
            choiceContext,
            cardPlay.Target!,
            DynamicVars["OpenWound"].BaseValue,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars["OpenWound"].UpgradeValueBy(3m); // 创伤 7-10
    }
}