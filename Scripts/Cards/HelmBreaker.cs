using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
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
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { MyKeywords.MuscleMemory };
    public HelmBreaker() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;

        NCreature playerNode = NCombatRoom.Instance.GetCreatureNode(Owner.Creature);
        NCreature targetNode = NCombatRoom.Instance.GetCreatureNode(target);

        float originalX = playerNode.GlobalPosition.X;
        float originalY = playerNode.GlobalPosition.Y;
        float offset = 30f;

        await CreatureCmd.TriggerAnim(Owner.Creature, "Attack", Owner.Character.AttackAnimDelay);
        await UpdatePlayerPosition(playerNode, targetNode.GlobalPosition.X - playerNode.GlobalPosition.X - offset - 50f, 0); // 向目标突刺

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
            await UpdatePlayerPosition(playerNode, offset, -600f, 0.7f); // 登

            await CreatureCmd.TriggerAnim(Owner.Creature, "Attack", Owner.Character.AttackAnimDelay);

            await UpdatePlayerPosition(playerNode, 0, 600f, 0.2f); // 兜割

            await UpdatePlayerPosition(playerNode, originalX - playerNode.GlobalPosition.X, originalY - playerNode.GlobalPosition.Y, 0.25f, false);

            await wound.TriggerMultiple(choiceContext, TraumaTriggerCount);
        } else {
            await UpdatePlayerPosition(playerNode, originalX - playerNode.GlobalPosition.X, originalY - playerNode.GlobalPosition.Y, 0.25f, false); // 回到原位
        }
    }

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