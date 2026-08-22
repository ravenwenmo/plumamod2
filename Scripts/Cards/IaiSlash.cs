using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;


namespace Pluma.Scripts;

// 居合：3费稀有保留（升级后）切割攻击牌，对单体敌人造成 6 点伤害。
// 命中后施加创伤翻倍标记（1层=2倍，升级后2层=3倍），主动打出时立即触发一层放大创伤。
// 在手牌中时：受到敌方怪物伤害前，本次伤害减半并自动向伤害来源打出最左侧一张本牌（每段伤害至多一张），
// 本牌被动打出只施加标记、不额外触发创伤，敌方攻击结算后的那层创伤被标记放大（不额外消耗创伤层数）。
[RegisterCard(typeof(PlumaCardPool))]
public class IaiSlash : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),   // 主动伤害 6
        ModCardVars.Int("AmplifyStacks", 1)   // 创伤翻倍层数：基础1，升级后2
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing, // 切割
        CardKeyword.Retain   // 保留
    };
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };
    public IaiSlash()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        // 主动伤害
        AttackCommand attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        // 命中判定：伤害成功造成（未被完全格挡）才施加标记
        bool hit = attack.Results.SelectMany(r => r).Any(r => r.UnblockedDamage > 0);
        if (!hit || target.IsDead) return;

        // 施加创伤翻倍标记：1层（×2），升级后 2层（×3）
        // 施加创伤翻倍标记，层数使用变量
        await PowerCmd.Apply<WoundAmplifyPower>(
            choiceContext,
            target,
            DynamicVars["AmplifyStacks"].BaseValue,
            base.Owner.Creature,
            this
        );

        if (cardPlay.IsAutoPlay)
        {
            // 自动打出：先移除旧减伤，再施加新的50层减伤，使本次伤害减半
            if (base.Owner.Creature.HasPower<IaiGuardPower>())
            {
                await PowerCmd.Remove<IaiGuardPower>(base.Owner.Creature);
            }

            await PowerCmd.Apply<IaiGuardPower>(
                choiceContext,
                base.Owner.Creature,
                50m,
                null,
                null
            );

            // 被动打出不额外触发创伤
            return;
        }
        // 主动打出：立即触发一层创伤（由创伤自身造成伤害，标记放大后清空）
        var wound = target.GetPower<OpenWoundPower>();
        if (wound != null)
        {
            await wound.TriggerMultiple(choiceContext, 1);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["AmplifyStacks"].UpgradeValueBy(1m);
    }
}
