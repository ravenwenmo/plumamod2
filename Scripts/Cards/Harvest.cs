using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models.Powers;using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 收获：对所有敌人造成伤害，若敌人血量低于一半则伤害提升至1.5倍。获得1层渐入佳境。本能。
[RegisterCard(typeof(PlumaCardPool))]
[RegisterCharacterStarterCard(typeof(PlumaCharacter), 1)]
public class Harvest : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Basic;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 关键词：本能
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.MuscleMemory
    };

    // 动态变量：伤害5，升级+4
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(5, ValueProp.Move)
    };

    public Harvest() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var enemies = CombatState?.HittableEnemies;
        if (enemies == null) return;

        // 对每个敌人单独处理
        foreach (var enemy in enemies)
        {
            decimal halfHp = enemy.MaxHp / 2m;
            decimal finalDamage = DynamicVars.Damage.BaseValue;
            if (enemy.CurrentHp < halfHp)
            {
                finalDamage = DynamicVars.Damage.BaseValue * 1.5m;
            }

            await DamageCmd.Attack(finalDamage)
                .FromCard(this, cardPlay)
                .Targeting(enemy)
                .Execute(choiceContext);
        }

        // 获得1层渐入佳境
        await PowerCmd.Apply<FlowState>(
            choiceContext,
            base.Owner.Creature,
            1,
            base.Owner.Creature,
            this
        );
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
    }
}