using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 让步：1费普通技能，获得10点格挡，对自己施加1层虚弱。升级后格挡+3。
[RegisterCard(typeof(PlumaCardPool))]
public class Concession : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 无额外关键词
    public override IEnumerable<CardKeyword> CanonicalKeywords => Enumerable.Empty<CardKeyword>();

    // 格挡变量（基础10，升级后+3）
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new BlockVar(10m, ValueProp.Move)
    };

    public Concession() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得格挡
        await CreatureCmd.GainBlock(
            base.Owner.Creature,
            DynamicVars.Block.BaseValue,
            ValueProp.Move,
            cardPlay
        );

        // 对自己施加1层虚弱
        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m); // 10 → 13
    }
}