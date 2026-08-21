
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Powers;

using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts;

// 标记性能力：龙舌兰持有时攻击意图改为群体伤害，并调整强化循环特性获取值和攻击循环回合数。
// 本能力虽然可叠加，但实际层数被限制为1。当重复施加导致层数超过1时，
// 会立即将层数削减回1，并触发额外效果：若龙舌兰处于蓄力状态，将其蓄力层数设置为3。
[RegisterPower]
public class BrotherAoePower : ModPowerTemplate
{
    // 强化循环每回合获得特性层数（可在此修改）
    public const int TraitPerTurn = 25;

    // 攻击循环持续回合数（可在此修改）
    public const int AttackIntentTurns = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter; // 改为可叠加

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/BrotherAoePower.png",
        BigIconPath: "res://pluma/images/powers/BrotherAoePower.png"
    );
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not BrotherAoePower || power.Owner != Owner) return;

        if (Amount > 1)
        {
            decimal excess = Amount - 1;
            await PowerCmd.Apply<BrotherAoePower>(
                choiceContext,
                Owner,
                -excess,
                Owner,
                cardSource
            );

            if (Owner.Monster is Brother brother && !brother.IntendsToAttack)
            {
                // 使用 Owner（Creature）操作
                if (Owner.HasPower<ChargingPower>())
                {
                    await PowerCmd.Remove<ChargingPower>(Owner);
                }
                await PowerCmd.Apply<ChargingPower>(
                    choiceContext,
                    Owner,
                    3m,
                    Owner,
                    null
                );
            }
        }
    }
}