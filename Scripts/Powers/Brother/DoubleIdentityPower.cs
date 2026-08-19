using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 双重身份：龙舌兰每次进入强化循环时（或获得本能力时正处于强化循环），若未冷却，
// 立即获得力量阈值一半的力量，随后进入冷却；龙舌兰进入攻击循环后冷却刷新，
// 从而每个连续的强化循环期间最多触发一次。
// 触发与刷新由 Brother.SwitchToPowerUpIntent / SwitchToAttackIntent 驱动。
[RegisterPower]
public class DoubleIdentityPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/DoubleIdentityPower.png",
        BigIconPath: "res://pluma/images/powers/DoubleIdentityPower.png"
    );

    // 冷却状态（1 = 冷却中）：本强化循环内已触发过力量获取
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Cooldown", 0)
    ];

    private bool IsOnCooldown
    {
        get => DynamicVars["Cooldown"].BaseValue >= 1;
        set => DynamicVars["Cooldown"].BaseValue = value ? 1 : 0;
    }

    // 获得能力时：若龙舌兰正处于强化循环且未冷却，立即触发一次
    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Monster is Brother brother && !brother.IntendsToAttack)
        {
            await TriggerStrengthGain();
        }
    }

    // 龙舌兰进入强化循环时（由 Brother.SwitchToPowerUpIntent 调用）
    public async Task TriggerOnPowerUpCycle()
    {
        await TriggerStrengthGain();
    }

    // 龙舌兰进入攻击循环时（由 Brother.SwitchToAttackIntent 调用），刷新冷却
    public void RefreshCooldown()
    {
        IsOnCooldown = false;
    }

    private async Task TriggerStrengthGain()
    {
        if (IsOnCooldown)
        {
            return;
        }

        IsOnCooldown = true;
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            Brother.STRENGTH_THRESHOLD / 2m,
            Owner,
            null
        );
    }
}
