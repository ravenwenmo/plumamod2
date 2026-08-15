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
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;

using MegaCrit.Sts2.Core.Combat;          // 提供 CombatManager
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;


namespace Pluma.Scripts;


// 注册卡牌到指定池（这里是无色）。如果要写自定义池看添加人物的开头
[RegisterCard(typeof(PlumaCardPool))]
// 注册成人物起始卡，后面是数量。不需要删除即可。
public class QuickDraw : ModCardTemplate
{
    // 基础耗能
    private const int energyCost = 1;
    // 卡牌类型
    private const CardType type = CardType.Attack;
    // 卡牌稀有度
    private const CardRarity rarity = CardRarity.Uncommon;
    // 目标类型（AnyEnemy表示任意敌人）
    private const TargetType targetType = TargetType.AllEnemies;
    // 是否在卡牌图鉴中显示
    private const bool shouldShowInCardLibrary = true;
    
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

    // 卡图资源
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
        // 卡框等，有需求自己添加。需要自行判断卡牌类型（攻击、技能、能力等）设置，建议写在基类里。
        // 如果使用自定义卡池，需要改下material，看添加人物章节的添加卡池部分
        // FramePath: "", // 卡牌背景
        // PortraitBorderPath: "", // 边框（状态牌感染使用的）
        // BannerTexturePath: "" // 横幅（不同类型）
    );

    // 卡牌基础数值
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new CalculationBaseVar(6m),      // 基础伤害
        new ExtraDamageVar(10m),          // 额外伤害（触发时加一次）
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((CardModel card, Creature? target) =>
        {
            // 获取当前战斗中的所有敌方单位（可攻击的敌人）
            var enemies = card.CombatState?.HittableEnemies;
            if (enemies == null) return 0m;

            // 统计本回合内所有敌人受到的攻击次数（来源：同阵营，包含自己）
            var totalAttacks = enemies.Sum(enemy =>
                CombatManager.Instance.History.Entries
                    .OfType<DamageReceivedEntry>()
                    .Count(e =>
                            e.Receiver == enemy &&
                            e.Result.Props.IsPoweredAttack() &&
                            e.HappenedThisTurn(card.CombatState) &&
                            e.Dealer != null &&
                            e.Dealer.Side == card.Owner.Creature.Side // 统计同阵营（包括自己）
                    )
            );
            if (totalAttacks == 0) return 1m;
            return 0;
        }
            )   
    ];

    
    public QuickDraw() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.CalculatedDamage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .Execute(choiceContext);
    }

    // 升级后的效果逻辑
    protected override void OnUpgrade()
    {
        //EnergyCost.UpgradeBy(-1);
        base.DynamicVars.ExtraDamage.UpgradeValueBy(4m);
        //base.DynamicVars.CalculatedDamage.UpgradeValueBy(2m);
    }
} 
