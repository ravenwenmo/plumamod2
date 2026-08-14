using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 金汤力：0费生成技能牌，鸡尾酒。目标为任意单位（敌人/自己/友方），实际效果由所选目标的阵营决定；右键仅在本机循环切换提示与光效，不影响实际效果。
// 效果：对敌人造成10点伤害并施加1层虚弱；对自己/友方造成1点穿透伤害后获得3层力量。升级后伤害+3并获得保留。
[RegisterCard(typeof(TokenCardPool))]
public class GinTonic : ModCardTemplate, IModRightClickableCard, ISpiritModeCard
{
    private const int energyCost = 0;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = false;

    // 内部模式枚举
    private enum SpiritMode
    {
        Self,   // 对自己
        Enemy,  // 对敌人
        Ally    // 对友方
    }

    private SpiritMode _mode = SpiritMode.Self; // 默认对自己

    // 当前模式的独立描述（供 SpiritModeDescriptionPatch 使用）
    public LocString SpiritModeDescription => _mode switch
    {
        SpiritMode.Self  => new LocString("cards", "PLUMA_CARD_GIN_TONIC_SELF_DESC"),
        SpiritMode.Enemy => new LocString("cards", "PLUMA_CARD_GIN_TONIC_ENEMY_DESC"),
        SpiritMode.Ally  => new LocString("cards", "PLUMA_CARD_GIN_TONIC_ALLY_DESC"),
        _ => new LocString("cards", "PLUMA_CARD_GIN_TONIC_SELF_DESC")
    };

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            yield return MyKeywords.Cocktail;
            yield return CardKeyword.Exhaust;
            // 升级后获得保留
            //if (base.IsUpgraded)
            {
                yield return CardKeyword.Retain;
            }
        }
    }

    // 伤害变量（基础10，升级+5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(12m, ValueProp.Move),
        ModCardVars.Int("FlexAmount", 5),
        ModCardVars.Int("WeakAmount", 1)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.Cocktail),
        HoverTipFactory.FromPower<WeakPower>()
    };

    // 卡牌类型固定为技能（不再随模式切换，避免多人模式下动作不同步）
    // 目标类型固定为"任意单位"：敌人、自己或友方均可选择，实际效果由所选目标的阵营决定
    public override TargetType TargetType => PlumaTargetTypes.AnyUnit;

    // 发光：敌人发红光，友方发金光，自己不发光（原版默认）
    protected override bool ShouldGlowRedInternal => _mode == SpiritMode.Enemy;
    protected override bool ShouldGlowGoldInternal => _mode == SpiritMode.Ally;

    public GinTonic() : base(energyCost, CardType.Skill, rarity, PlumaTargetTypes.AnyUnit, shouldShowInCardLibrary)
    {
    }

    // 右键本地预检：始终允许
    public bool CanHandleRightClickLocal(ModRightClickContext context) => true;

    // 右键循环切换模式
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        _mode = _mode switch
        {
            SpiritMode.Self  => SpiritMode.Enemy,
            SpiritMode.Enemy => SpiritMode.Ally,
            SpiritMode.Ally  => SpiritMode.Self,
            _ => SpiritMode.Self
        };

        // 立即刷新发光与描述：重跑原版 UpdateCard（含 ShouldGlowRed/GoldInternal 判定），
        // 描述由 SpiritModeDescriptionPatch 按当前 _mode 替换。类型与目标类型已固定，无需刷新类型标签。
        if (NPlayerHand.Instance?.GetCardHolder(this) is NHandCardHolder holder)
        {
            holder.UpdateCard();
        }

        // 可选：弹出提示（如果 RitsuToastService 不可用，可删除或替换为 GD.Print）
        // RitsuToastService.ShowInfo(_mode == SpiritMode.Self ? "目标：自己" : _mode == SpiritMode.Enemy ? "目标：敌人" : "目标：友方");

        await Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await FlaskVfxHelper.PlaySimpleThrow(Owner.Creature, cardPlay.Target);
        switch (SpiritTargeting.Resolve(cardPlay.Target, base.Owner.Creature))
        {
            case SpiritTargetBranch.Self:
                // 先失去 1 点生命（穿透伤害）
                //await CreatureCmd.Damage(choiceContext, base.Owner.Creature, 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                // 获得 5 层力量
                await PowerCmd.Apply<FlexPotionPower>(choiceContext, base.Owner.Creature, DynamicVars["FlexAmount"].BaseValue,
                    base.Owner.Creature, this);
                // ---- 旧占位效果（对自己施加1层虚弱，已注释保留）----
                // await PowerCmd.Apply<WeakPower>(choiceContext, base.Owner.Creature, 1,
                //     base.Owner.Creature, this);
                break;

            case SpiritTargetBranch.Enemy:
                // 对敌人造成 {Damage} 点攻击伤害，并施加 1 层虚弱
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(cardPlay.Target!)
                    .Execute(choiceContext);
                await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakAmount"].BaseValue,
                    base.Owner.Creature, this);
                break;

            case SpiritTargetBranch.Ally:
                // 先让目标失去 1 点生命（穿透伤害）
                //await CreatureCmd.Damage(choiceContext, cardPlay.Target!, 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                // 获得 5 层力量
                await PowerCmd.Apply<FlexPotionPower>(choiceContext, cardPlay.Target, DynamicVars["FlexAmount"].BaseValue,
                    base.Owner.Creature, this);
                // ---- 旧占位效果（对友方施加1层虚弱，已注释保留）----
                // await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1,
                //     base.Owner.Creature, this);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m); // 伤害 10 → 15
    }
}
