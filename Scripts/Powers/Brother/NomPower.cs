using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 嚼：龙舌兰攻击斩杀敌人时，提升3点最大生命值，然后消耗1层。可叠加。
[RegisterPower]
public class NomPower : ModPowerTemplate
{
    // 每次斩杀提升的最大生命值
    private const int MaxHpGainPerKill = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/NomPower.png",
        BigIconPath: "res://pluma/images/powers/NomPower.png"
    );

    // 注：不能用 AfterAttack 钩子——它只在 DamageCmd.Attack / AttackContext（卡牌出牌）路径触发，
    // 而龙舌兰的攻击走的是 CreatureCmd.Damage（Brother.TakeTurn），该路径只触发 AfterDamageGiven，
    // 因此之前的 AfterAttack 实现对龙舌兰的攻击根本不会执行（提升最大生命值与消耗层数都失效）。
    // 斩杀结算改挂在 AfterDamageGiven：每个被龙舌兰斩杀的目标结算一次（+3 最大生命值并消耗1层）。
    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // 只结算龙舌兰自己的攻击
        if (dealer != Owner) return;
        if (Amount <= 0) return;
        if (!result.WasTargetKilled) return;

        // 提升最大生命值（当前生命值不变）。
        // CreatureCmd.SetMaxHp 对宠物同样生效：纯模型变更，无玩家/宠物限制，
        // AfterAddedToRoom 恢复龙舌兰最大生命值用的就是它。
        // 用 try-catch 保护：即使持久化或 UI 刷新抛异常，也不阻断下方的能力消耗。
        try
        {
            int oldMax = Owner.MaxHp;
            int newMax = oldMax + MaxHpGainPerKill;
            await CreatureCmd.SetMaxHp(Owner, newMax);

            if (Owner.PetOwner != null)
            {
                // 同步持久化数据（BrotherRelic 上的 SavedMaxHp），否则下场战斗会丢失
                BrotherStateData.SetHp(Owner.PetOwner, Owner.CurrentHp, newMax);
            }

            GD.Print($"[NomPower] kill by Brother: MaxHp {oldMax} -> {Owner.MaxHp}, stacks left {Amount - 1}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NomPower] max HP gain failed: {ex}");
        }

        // 消耗一层能力（放在最后，确保前面的异常不会阻止消耗）
        await PowerCmd.Decrement(this);
    }
}
