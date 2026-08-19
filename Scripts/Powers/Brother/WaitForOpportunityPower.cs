using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;


namespace Pluma.Scripts;

// 伺机而动：龙舌兰处于蓄力循环时，若龙舌兰或羽毛笔受到伤害，对伤害来源造成
// （龙舌兰基础攻击力 + 当前力量） × 能力层数 的伤害。伤害来源视为龙舌兰。
[RegisterPower]
public class WaitForOpportunityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/WaitForOpportunity.png",
        BigIconPath: "res://pluma/images/powers/WaitForOpportunity.png"
    );

    public override async Task BeforeDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 仅当伤害来源存在时触发
        if (dealer == null)
        {
            return;
        }

        // 获取龙舌兰
        if (Owner.Monster is not Brother brother)
        {
            return;
        }

        // 只在龙舌兰处于蓄力循环（非攻击循环）时触发
        if (brother.IntendsToAttack)
        {
            return;
        }

        // 判断受伤目标是否为龙舌兰或羽毛笔（玩家操控角色）
        bool targetIsBrother = target == Owner;
        bool targetIsPlayer = target == Owner.PetOwner?.Creature;

        if (!targetIsBrother && !targetIsPlayer)
        {
            return;
        }

        // 计算伤害值：(基础攻击力 + 当前力量) × 能力层数 / 100m
        int strengthAmount = Owner.GetPowerAmount<StrengthPower>();
        decimal baseDamage = Brother.BasicDamage + strengthAmount;
        decimal totalDamage = baseDamage * (decimal)Amount / 100m;

        // 对伤害来源造成伤害，来源视为龙舌兰
        await CreatureCmd.Damage(
            choiceContext,
            dealer,
            totalDamage,
            ValueProp.Unpowered,
            Owner
        );
    }
}