using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 敲：0费普通攻击牌。造成1点伤害2次。每当你打出敲，本场战斗中所有敲卡牌的攻击段数增加1次。升级后初始段数变为3。
[RegisterCard(typeof(PlumaCardPool))]
public class Knock : ModCardTemplate
{
    private const int energyCost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害固定1点，段数基础2（升级后3）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(1m, ValueProp.Move),
        ModCardVars.Int("Hits", 2)
    };

    public Knock()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target == null) return;

        decimal damage = DynamicVars.Damage.BaseValue;
        int hits = DynamicVars["Hits"].IntValue;

        // 先按当前段数造成伤害。所有段数合并为一条攻击命令（WithHitCount），
        // 保证活力（VigorPower）等每次攻击消耗的能力对每段伤害都生效，
        // 与旋风斩（Whirlwind）等原版多段牌保持一致。
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(hits)
            .Execute(choiceContext);

        // 然后让本场战斗中所有敲卡牌的攻击段数+1
        int buff = 1;
        IEnumerable<Knock> allKnocks = base.Owner.PlayerCombatState.AllCards.OfType<Knock>();
        foreach (Knock knock in allKnocks)
        {
            knock.DynamicVars["Hits"].BaseValue += buff;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m); // 2 → 3
    }
}