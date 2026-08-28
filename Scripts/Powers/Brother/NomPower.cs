using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 嚼：龙舌兰攻击斩杀敌人时，提升3/4点最大生命值。不可叠加。
[RegisterPower]
public class NomPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public int Counter { set; get; } = 1;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/NomPower.png",
        BigIconPath: "res://pluma/images/powers/NomPower.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Counter", Counter)
    ];

    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        Counter += target.GetPower<NomPower>()?.Counter ?? 0;
        DynamicVars["Counter"].BaseValue = Counter;
    }

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
        if (!result.WasTargetKilled) return;
        Counter--;
        DynamicVars["Counter"].BaseValue = Counter;
        if (Counter > 0) return;

        // 提升最大生命值（当前生命值不变）。
        // CreatureCmd.SetMaxHp 对宠物同样生效：纯模型变更，无玩家/宠物限制，
        // AfterAddedToRoom 恢复龙舌兰最大生命值用的就是它。
        // 用 try-catch 保护：即使持久化或 UI 刷新抛异常，也不阻断下方的能力消耗。
        try
        {
            int oldMax = Owner.MaxHp;
            int hpGain = (int)Amount;          // 提升的最大生命值
            int newMax = oldMax + hpGain;

            //await CreatureCmd.SetMaxHp(Owner, newMax);
            // 为什么上面的生效但是下面的用heal会不生效
            //await CreatureCmd.SetCurrentHp(Owner, Owner.CurrentHp + hpGain);
            
            //await CreatureCmd.GainMaxHp(Owner, hpGain);
            //await CreatureCmd.Heal(Owner, hpGain);
            
            await CreatureCmd.SetMaxHp(Owner, newMax);
            await CreatureCmd.SetCurrentHp(Owner, Owner.CurrentHp + hpGain);

            // 手动补上治疗绿字与十字特效
            if (hpGain > 0)
            {
                // 治疗音效
                SfxCmd.Play("event:/sfx/heal");
                // 十字治疗特效
                VfxCmd.PlayOnCreatureCenter(Owner, "vfx/vfx_cross_heal");
                // 治疗绿字
                Owner.GetVfxContainer()?.AddChildSafely(NHealNumVfx.Create(Owner, hpGain));
            }

            
            if (Owner.PetOwner != null)
            {
                BrotherStateData.SetHp(Owner.PetOwner, Owner.CurrentHp, newMax);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[NomPower] max HP gain failed: {ex}");
        }
        
        // 移除此能力
        await PowerCmd.Remove(this);
    }
}
