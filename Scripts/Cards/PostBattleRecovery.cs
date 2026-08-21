using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using Pluma.Scripts;
using Pluma.Scripts.Monsters; // 需要访问 BrotherStateData
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 【牌名待定】1费本能罕见技能。作战结束后龙舌兰回复7血。每打出一次该牌费用+1（本场战斗内）。升级后初始0费。
[RegisterCard(typeof(PlumaCardPool))]
public class PostBattleRecovery : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon; // 罕见
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory // 本能
    };

    public PostBattleRecovery()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int healAmount = 7;

        // 持久化累积待回复生命值
        BrotherStateData.AddPendingHeal(base.Owner, healAmount);

        // 给玩家自身施加计数显示能力
        await PowerCmd.Apply<PendingHealPower>(
            choiceContext,
            base.Owner.Creature,
            healAmount,
            base.Owner.Creature,
            this
        );

        // 本场战斗内费用 +1
        base.EnergyCost.AddThisCombat(1);
    }

    protected override void OnUpgrade()
    {
        base.EnergyCost.UpgradeBy(-1); // 1费 → 0费
    }
}