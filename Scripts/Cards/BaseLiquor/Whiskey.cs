using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 威士忌：0费生成技能牌，基酒。目标为任意单位（敌人/自己/友方），实际效果由所选目标的阵营决定；右键仅在本机循环切换提示与光效，不影响实际效果。
// 效果：对敌人造成3点伤害并施加1层虚弱；对自己/友方造成1点穿透伤害后获得3层覆甲。升级后伤害+3。
[RegisterCard(typeof(TokenCardPool))]
public class Whiskey : ModCardTemplate, IModRightClickableCard, IBaseSpiritCard, ISpiritModeCard, IBaseSpiritRelatedCard
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
    public LocString SpiritModeDescription => DescriptionForMode(_mode);

    // 瞄准预览：按瞄准目标阵营返回对应分支描述（供 SpiritModeDescriptionPatch 使用）
    public LocString GetSpiritDescriptionFor(SpiritTargetBranch branch) => branch switch
    {
        SpiritTargetBranch.Self  => DescriptionForMode(SpiritMode.Self),
        SpiritTargetBranch.Enemy => DescriptionForMode(SpiritMode.Enemy),
        SpiritTargetBranch.Ally  => DescriptionForMode(SpiritMode.Ally),
        _ => DescriptionForMode(SpiritMode.Self)
    };

    private LocString DescriptionForMode(SpiritMode mode) => mode switch
    {
        SpiritMode.Self  => new LocString("cards", "PLUMA_CARD_WHISKEY_SELF_DESC"),
        SpiritMode.Enemy => new LocString("cards", "PLUMA_CARD_WHISKEY_ENEMY_DESC"),
        SpiritMode.Ally  => new LocString("cards", "PLUMA_CARD_WHISKEY_ALLY_DESC"),
        _ => new LocString("cards", "PLUMA_CARD_WHISKEY_SELF_DESC")
    };

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 右键 SpiritMode → 预览分支（Self/Enemy/Ally 一一对应），
    // 供 hover tip 中的鸡尾酒预览牌跟随切换。
    private SpiritTargetBranch CurrentSpiritBranch => _mode switch
    {
        SpiritMode.Self  => SpiritTargetBranch.Self,
        SpiritMode.Enemy => SpiritTargetBranch.Enemy,
        SpiritMode.Ally  => SpiritTargetBranch.Ally,
        _ => SpiritTargetBranch.Self
    };

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.BaseSpirit,
        CardKeyword.Exhaust,
        CardKeyword.Retain
    };

    // 伤害变量（基础3，无升级变化）
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(6m, ValueProp.Move),          // 原有伤害变量，保持不变
        ModCardVars.Int("WeakAmount", 1),           // 原有虚弱层数
        new BlockVar(10m, ValueProp.Move),          // 友方/自己格挡值
        ModCardVars.Int("PetBlockAmount", 15)       // 龙舌兰格挡值
    };
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.BaseSpirit),
        HoverTipFactory.FromPower<WeakPower>(),
        // 预览对应的鸡尾酒牌
        SpiritModeHoverPreview.FromCard<OldFashioned>(CurrentSpiritBranch)
    };

    // 卡牌类型固定为技能（不再随模式切换，避免多人模式下动作不同步）
    // 目标类型固定为"任意单位"：敌人、自己或友方均可选择，实际效果由所选目标的阵营决定
    public override TargetType TargetType => PlumaTargetTypes.AnyUnit;

    // 发光：瞄准预览时按目标阵营发光（敌人红光、友方金光、自己无特殊光），
    // 未瞄准时按右键 SpiritMode 发光
    protected override bool ShouldGlowRedInternal =>
        CardAimPreview.GetAimBranchFor(this) is SpiritTargetBranch aim
            ? aim == SpiritTargetBranch.Enemy
            : _mode == SpiritMode.Enemy;
    protected override bool ShouldGlowGoldInternal =>
        CardAimPreview.GetAimBranchFor(this) is SpiritTargetBranch aim
            ? aim == SpiritTargetBranch.Ally
            : _mode == SpiritMode.Ally;

    public Whiskey() : base(energyCost, CardType.Skill, rarity, PlumaTargetTypes.AnyUnit, shouldShowInCardLibrary)
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

            // 右键切换时若悬浮提示正打开（指针仍在本牌上），立即重建悬浮提示，
            // 让其中的鸡尾酒预览随新的 SpiritMode 分支刷新。
            if (holder.GetGlobalRect().HasPoint(holder.GetGlobalMousePosition()))
            {
                NHoverTipSet.Remove(holder);
                NHoverTipSet.CreateAndShow(holder, HoverTips)?.SetAlignmentForCardHolder(holder);
            }
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
                await CreatureCmd.Damage(choiceContext, base.Owner.Creature, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, base.Owner.Creature);

                // 获得格挡
                await CreatureCmd.GainBlock(
                    base.Owner.Creature,
                    DynamicVars["Block"].BaseValue,
                    ValueProp.Move,
                    cardPlay
                );
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
                await CreatureCmd.Damage(choiceContext, cardPlay.Target!, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, base.Owner.Creature);

                // 龙舌兰：获得 15 点格挡
                if (cardPlay.Target != null && cardPlay.Target.IsPet)
                {
                    await CreatureCmd.GainBlock(
                        cardPlay.Target,
                        DynamicVars["PetBlockAmount"].BaseValue,
                        ValueProp.Move,
                        cardPlay
                    );
                    break;
                }

                // 友方玩家：获得 10 点格挡
                await CreatureCmd.GainBlock(
                    cardPlay.Target!,
                    DynamicVars["Block"].BaseValue,
                    ValueProp.Move,
                    cardPlay
                );
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m); // 伤害 3 → 6
    }
}
