using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 兜割：0费攻击，造成6点伤害，并引爆目标至多7层创伤。升级后伤害+3。
[RegisterCard(typeof(PlumaCardPool))]
public class HelmBreaker : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.AnyEnemy;
    private const bool shouldShowInCardLibrary = true;

    // 兜割引爆创伤的触发次数上限（血条预测也读取此常量）
    public const int TraumaTriggerCount = 7;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new DamageVar(18m, ValueProp.Move)
    };
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };
    public HelmBreaker() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        // 目标可能在本牌的前一段结算里已经被打死了：同一张牌在一次出牌里打多段时
        // （华彩附魔的重放、原版 BaseReplayCount / Spiral 等），Target 是整次出牌解析一次、
        // 各段共用的（见 CardModel.OnPlayWrapper 的 playCount 循环），而生物死亡时引擎会立刻
        // 把它的节点从 NCombatRoom 的活节点表里摘掉（CreatureCmd 死亡处理 → RemoveCreatureNode，
        // 之后 GetCreatureNode 返回 null）。
        // 旧代码直接解引用 targetNode.GlobalPosition 会抛 NullReferenceException，异常会中断
        // 整个 PlayCardAction：卡面停在 AnimMultiCardPlay 摆好的屏幕中央不再弃牌，重放那段的
        // 伤害/创伤引爆也一起丢失。
        // 所以这里把"位移演出"整块做成可跳过的：拿不到节点就只结算伤害，正常情况行为不变。
        NCreature? playerNode = NCombatRoom.Instance?.GetCreatureNode(Owner.Creature);
        NCreature? targetNode = target.IsAlive ? NCombatRoom.Instance?.GetCreatureNode(target) : null;
        bool canTween = IsNodeUsable(playerNode) && IsNodeUsable(targetNode);

        float originalX = 0f;
        float originalY = 0f;
        float offset = 30f;

        if (canTween)
        {
            originalX = playerNode!.GlobalPosition.X;
            originalY = playerNode!.GlobalPosition.Y;

            await CreatureCmd.TriggerAnim(Owner.Creature, "Attack", Owner.Character.AttackAnimDelay);
            await UpdatePlayerPosition(playerNode!, targetNode!.GlobalPosition.X - playerNode!.GlobalPosition.X - offset - 50f, 0); // 向目标突刺
        }

        // 基础伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithNoAttackerAnim()
            .Execute(choiceContext);

        // 引爆目标身上的创伤（最多7次），伤害来源与普通创伤一致
        var wound = target.Powers.OfType<OpenWoundPower>().FirstOrDefault();
        if (wound != null && wound.Amount > 0)
        {
            if (canTween)
            {
                await UpdatePlayerPosition(playerNode!, offset, -600f, 0.7f); // 登

                await CreatureCmd.TriggerAnim(Owner.Creature, "Attack", Owner.Character.AttackAnimDelay);

                await UpdatePlayerPosition(playerNode!, 0, 600f, 0.2f); // 兜割

                await ReturnToOrigin(playerNode!, originalX, originalY);
            }

            await wound.TriggerMultiple(choiceContext, TraumaTriggerCount);
        }
        else if (canTween)
        {
            await ReturnToOrigin(playerNode!, originalX, originalY); // 回到原位
        }
    }

    // 节点存在且未被释放（生物被移出战斗后节点会被摘掉并稍后释放）
    private static bool IsNodeUsable(NCreature? node)
        => node != null && GodotObject.IsInstanceValid(node);

    private async Task ReturnToOrigin(NCreature playerNode, float originalX, float originalY)
        => await UpdatePlayerPosition(playerNode, originalX - playerNode.GlobalPosition.X, originalY - playerNode.GlobalPosition.Y, 0.25f, false);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m); // 6 → 9
    }

    private async Task UpdatePlayerPosition(NCreature creatureNode, float x, float y, float duration = 0.25f, bool waitForCompletion = true)
    {
        Tween tween = NCombatRoom.Instance.CreateTween()
                    .SetParallel()
                    .SetEase(Tween.EaseType.In)
					.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(creatureNode, "global_position:x", creatureNode.GlobalPosition.X + x, duration);
        tween.TweenProperty(creatureNode, "global_position:y", creatureNode.GlobalPosition.Y + y, duration);

        if (tween != null && waitForCompletion)
		{
			await tween.AwaitFinished(NCombatRoom.Instance);
		}
    } 
}