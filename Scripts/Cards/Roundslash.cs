using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts.Cards;

// 回旋切割：1费本能切割攻击。对所有敌人造成3点伤害。若上一张打出的牌是切割牌，则切割计数变为5层。升级后伤害提升至6。
[RegisterCard(typeof(PlumaCardPool))]
public class Roundslash : ModCardTemplate
{
    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害变量：基础3，升级后+3
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move)
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        //MyKeywords.MuscleMemory, // 本能
        MyKeywords.Slashing      // 切割
    };

    public Roundslash()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = base.Owner;
        
        // 对所有敌人造成伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
        // 检查上一张打出的牌是否为切割牌：
        // 若当前连击计数 > 0，说明上一张是切割牌（非切割且无利刃形态时会清零），
        // 此时将计数直接设为上限5。
        int currentCombo = SlashingComboSingleton.GetPlayerComboCount(player);
        if (currentCombo > 0)
        {
            SlashingComboSingleton.Instance?.SetComboCount(player, 5);
        }

    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m); // 3 → 6
        AddKeyword(MyKeywords.MuscleMemory);
    }
}