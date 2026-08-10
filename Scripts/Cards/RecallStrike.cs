using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Combat;


namespace Pluma.Scripts;

// 回钩：1费普通攻击，造成3点伤害，将上一张打出的牌放回手牌。消耗。升级后伤害+2。
[RegisterCard(typeof(PlumaCardPool))]
public class RecallStrike : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(3m, ValueProp.Move) };

    public RecallStrike() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 1. 造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        // 2. 从战斗历史中找出上一张打出的牌（非自身）
        var history = CombatManager.Instance.History.CardPlaysStarted;
        var previousCardPlay = history.Reverse()
            .FirstOrDefault(entry => entry.CardPlay != cardPlay)?
            .CardPlay;

        if (previousCardPlay?.Card != null)
        {
            // 3. 将那张牌放入手牌（参照 Headbutt 的用法）
            await CardPileCmd.Add(previousCardPlay.Card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 3 → 5
    }
}