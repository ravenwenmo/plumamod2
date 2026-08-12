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
using System.Reflection; // 引入反射
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Pluma.Scripts;

// 边车：0费生成技能牌，鸡尾酒。右键循环切换模式：自己 -> 敌人 -> 友方。
// 效果：对敌人造成10点伤害并施加1层虚弱；对自己/友方造成1点穿透伤害后获得3层敏捷。升级后伤害+3并获得保留。
[RegisterCard(typeof(TokenCardPool))]
public class Sidecar : ModCardTemplate, IModRightClickableCard, ISpiritModeCard
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
        SpiritMode.Self  => new LocString("cards", "PLUMA_CARD_SIDECAR_SELF_DESC"),
        SpiritMode.Enemy => new LocString("cards", "PLUMA_CARD_SIDECAR_ENEMY_DESC"),
        SpiritMode.Ally  => new LocString("cards", "PLUMA_CARD_SIDECAR_ALLY_DESC"),
        _ => new LocString("cards", "PLUMA_CARD_SIDECAR_SELF_DESC")
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
            if (base.IsUpgraded)
            {
                yield return CardKeyword.Retain;
            }
        }
    }

    // 伤害变量（基础10，升级+5）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromKeyword(MyKeywords.Cocktail),
        HoverTipFactory.FromPower<WeakPower>()
    };

    // 卡牌类型固定为技能
    //public override CardType Type => CardType.Skill;
    // 根据模式动态返回卡牌类型：对敌人为攻击牌，对自己/友方为技能牌
    public override CardType Type => _mode == SpiritMode.Enemy ? CardType.Attack : CardType.Skill;
    // 根据模式动态返回目标类型
    public override TargetType TargetType
    {
        get
        {
            return _mode switch
            {
                SpiritMode.Self  => TargetType.Self,
                SpiritMode.Enemy => TargetType.AnyEnemy,
                SpiritMode.Ally  => TargetType.AnyAlly,
                _ => TargetType.Self
            };
        }
    }

    // 发光：敌人发红光，友方发金光，自己不发光（原版默认）
    protected override bool ShouldGlowRedInternal => _mode == SpiritMode.Enemy;
    protected override bool ShouldGlowGoldInternal => _mode == SpiritMode.Ally;

    public Sidecar() : base(energyCost, CardType.Skill, rarity, TargetType.Self, shouldShowInCardLibrary)
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

        // 立即刷新发光：重跑原版 UpdateCard（含 ShouldGlowRed/GoldInternal 判定），RitsuLib 描边补丁会一并触发
        if (NPlayerHand.Instance?.GetCardHolder(this) is NHandCardHolder holder)
        {
            holder.UpdateCard();

            // 尝试刷新类型标签（技能/攻击）
            var cardNode = holder.CardNode;
            if (cardNode != null)
            {
                var method = typeof(NCard).GetMethod("UpdateTypePlaque",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                method?.Invoke(cardNode, null);
            }
        }

        // 可选：弹出提示（如果 RitsuToastService 不可用，可删除或替换为 GD.Print）
        // RitsuToastService.ShowInfo(_mode == SpiritMode.Self ? "目标：自己" : _mode == SpiritMode.Enemy ? "目标：敌人" : "目标：友方");

        await Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        switch (_mode)
        {
            case SpiritMode.Self:
                // 先失去 1 点生命（穿透伤害）
                await CreatureCmd.Damage(choiceContext, base.Owner.Creature, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                // 获得 3 层敏捷
                await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner.Creature, 3,
                    base.Owner.Creature, this);
                // ---- 旧效果（对自己获得1点能量，已注释保留）----
                // await PlayerCmd.GainEnergy(1, base.Owner);
                break;

            case SpiritMode.Enemy:
                // 对敌人造成 {Damage} 点攻击伤害，并施加 1 层虚弱
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(cardPlay.Target!)
                    .Execute(choiceContext);
                await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1,
                    base.Owner.Creature, this);
                break;

            case SpiritMode.Ally:
                // 先让目标失去 1 点生命（穿透伤害）
                await CreatureCmd.Damage(choiceContext, cardPlay.Target!, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                // 获得 3 层敏捷
                await PowerCmd.Apply<DexterityPower>(choiceContext, cardPlay.Target, 3,
                    base.Owner.Creature, this);
                // ---- 旧效果（对友方获得1点能量，已注释保留）----
                // await PlayerCmd.GainEnergy(1, cardPlay.Target!.Player);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m); // 伤害 10 → 15
    }
}
