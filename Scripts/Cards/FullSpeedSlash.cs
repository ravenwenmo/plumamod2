using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 全速斩击：2费攻击牌，造成 4 点伤害 3 次（升级后伤害 +1）。
// 额外段数 = 渐入佳境层数 / 2（向下取整）；攻击结算完之后，失去与额外段数等量的渐入佳境层数
// （先结算再扣，本牌自己的几段仍吃得到渐入佳境 ≥12 层的 +10% 伤害）。
// 卡面用 CalculatedVar("TotalHits") 实时算出总段数 = 基础段数 + 渐入佳境层数/2，玩家能直接看到会打几下。
[RegisterCard(typeof(PlumaCardPool))]
public class FullSpeedSlash : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 卡面 {TotalHits:diff()}：本次实际总段数（基础段数 + 额外段数）
    private const string TotalHitsKey = "TotalHits";
    // 卡面 {Decline:diff()}：打出后失去的渐入佳境层数（升级后减少）
    private const string DeclineKey = "Decline";
    // 基础段数
    private const int BaseHits = 3;
    private const int Decline = 3;
    // 每多少层渐入佳境换 1 段额外攻击
    private const int FlowStacksPerExtraHit = 2;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 伤害、基础段数、每层额外段数、以及卡面显示的总段数。
    // TotalHits 用 CalculatedVar：值在每次刷新卡面时现算（CalculationBase + CalculationExtra × 乘数），
    // 所以渐入佳境层数一变，卡面段数也跟着变。做法与本体「弹幕 Barrage」的 {CalculatedHits} 一致。
    // 注意：CalculationBase / CalculationExtra 是固定名字的单例变量，DynamicVarSet 遇到重名会直接抛
    // "DynamicVarSet contains duplicate key"，导致整张牌建不出来（卡牌不显示）。所以 {Decline} 必须用
    // 普通的具名 DynamicVar，绝不能再来一个 CalculationBaseVar。
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(4m, ValueProp.Move),      // 基础伤害：升级前4，升级后5
        new CalculationBaseVar(BaseHits),       // 计算基数 = 基础段数
        new CalculationExtraVar(1m),            // 每 1 个乘数单位 +1 段
        new CalculatedVar(TotalHitsKey).WithMultiplier(GetFlowExtraHitMultiplier),
        new DynamicVar(DeclineKey, Decline),    // 打出后失去的渐入佳境层数
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Slashing      // 切割
    };

    public FullSpeedSlash() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // CalculatedVar 的乘数：渐入佳境层数 / 2（向下取整）。
    // WithMultiplier 要求委托不能捕获模型实例（否则抛异常），所以必须是静态方法。
    private static decimal GetFlowExtraHitMultiplier(CardModel card, Creature? target)
    {
        return GetExtraHits(card);
    }

    // 额外段数 = 渐入佳境层数 / 2（向下取整）。卡面计算与实战结算共用同一份公式，避免两处写歪。
    private static int GetExtraHits(CardModel? card)
    {
        var creature = card?.Owner?.Creature;
        if (creature == null) return 0;
        return creature.GetPowerAmount<FlowState>() / FlowStacksPerExtraHit;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        decimal baseDamage = DynamicVars.Damage.BaseValue;
        int baseHits = DynamicVars.CalculationBase.IntValue;

        // 额外段数 = 渐入佳境层数 / 2（向下取整）
        int extraHits = GetExtraHits(this);

        // 基础段数与额外段数合并为一条攻击命令（WithHitCount），
        // 保证活力（VigorPower）等每次攻击消耗的能力对每段伤害都生效，
        // 与旋风斩（Whirlwind）等原版多段牌保持一致。
        await DamageCmd.Attack(baseDamage)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitCount(baseHits + extraHits)
            .Execute(choiceContext);

        // 攻击结算完再失去层数（数量读卡面上的 {Decline}，这样升级把它降到 2 时实际也只掉 2 层）。
        // 顺序很重要：放在伤害之后，本牌自己那几段仍能吃到「渐入佳境 ≥ 12 层 +10% 伤害」的加成
        // （FlowState.ModifyDamageMultiplicative 在伤害结算时读层数），扣层数只影响后续卡牌。
        // 注意：负数量走 PowerCmd.Apply 是本体标准的扣层数写法（如 Friendship / Malaise）。
        int decline = DynamicVars[DeclineKey].IntValue;
        if (base.Owner.Creature.GetPowerAmount<FlowState>() > decline)
        {
            await PowerCmd.Apply<FlowState>(
                choiceContext,
                base.Owner.Creature,
                -decline,
                base.Owner.Creature,
                this
            );
        }
        else
        {
            // 不直接 Remove，而是把层数扣到 0（PowerCmd.Apply 传负数是本体标准的扣层数写法）。
            // 注意：层数归 0 后本体依然会自动移除能力（PowerCmd.ModifyAmount 末尾的 ShouldRemoveDueToAmount），
            // 所以"留一个 0 层的能力"是做不到的——真正兜住打折的是 FlowState.AfterRemoved：
            // 能力消失时会把当时还在打折的牌转成卡片自身的临时降费，已抽到的本能牌本回合不会恢复原价。
            int current = base.Owner.Creature.GetPowerAmount<FlowState>();
            if (current > 0)
            {
                await PowerCmd.Apply<FlowState>(
                    choiceContext,
                    base.Owner.Creature,
                    -current,
                    base.Owner.Creature,
                    this
                );
            }
        }
    }

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<FlowState>()
    };

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m); // 伤害 4 → 5
        DynamicVars[DeclineKey].UpgradeValueBy(-1m); // 失去层数 3 → 2
    }
}
