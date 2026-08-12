using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using System.Reflection; // 引入反射
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Pluma.Scripts;

// 大都会：0费生成技能牌，鸡尾酒。右键循环切换模式：自己 -> 敌人 -> 友方。
// 效果占位：对自己造成1点伤害并施加1层虚弱；对敌人造成1点伤害并施加1层虚弱；对友方造成1点伤害并施加1层虚弱。
[RegisterCard(typeof(TokenCardPool))]
public class Cosmopolitan : ModCardTemplate, IModRightClickableCard
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

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public override IEnumerable<CardKeyword> CanonicalKeywords => new[]
    {
        MyKeywords.Cocktail,
        CardKeyword.Exhaust
    };

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

    public Cosmopolitan() : base(energyCost, CardType.Skill, rarity, TargetType.Self, shouldShowInCardLibrary)
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
                // 对自己造成1点穿透伤害，并施加1层虚弱
                await CreatureCmd.Damage(choiceContext, base.Owner.Creature, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                await PowerCmd.Apply<WeakPower>(choiceContext, base.Owner.Creature, 1,
                    base.Owner.Creature, this);
                break;

            case SpiritMode.Enemy:
                // 对敌人造成1点攻击伤害，并施加1层虚弱
                await DamageCmd.Attack(1m)
                    .FromCard(this, cardPlay)
                    .Targeting(cardPlay.Target!)
                    .Execute(choiceContext);
                await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1,
                    base.Owner.Creature, this);
                break;

            case SpiritMode.Ally:
                // 对友方造成1点穿透伤害，并施加1层虚弱
                await CreatureCmd.Damage(choiceContext, cardPlay.Target!, 1,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null);
                await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1,
                    base.Owner.Creature, this);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        // Token牌无升级
    }
}
