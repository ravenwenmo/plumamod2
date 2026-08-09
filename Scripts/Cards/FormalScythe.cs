using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 制式镰刀：1费攻击牌，造成7点伤害，给一张手牌添加切割关键词。升级后伤害+3。
[RegisterCard(typeof(PlumaCardPool))]
public class FormalScythe : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );


    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(7m, ValueProp.Move)
    };

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.Slashing)
    };

    public FormalScythe() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 先造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 延迟一帧，确保伤害结算完毕
        await Task.Delay(1);

        // 使用自定义提示选择一张手牌
        var selectPrompt = new LocString("cards", "PLUMA_CARD_FORMAL_SCYTHE.selectPrompt");
        var selected = await CardSelectCmd.FromHand(
            context: choiceContext,
            player: base.Owner,
            prefs: new CardSelectorPrefs(selectPrompt, 1),
            filter: c => c.Type == CardType.Attack && !c.Keywords.Contains(MyKeywords.Slashing),
            source: this
        );
        var targetCard = selected.FirstOrDefault();
        if (targetCard != null)
        {
            CardCmd.ApplyKeyword(targetCard, MyKeywords.Slashing);
        }
    }
    
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m); // 7 → 10
    }
}