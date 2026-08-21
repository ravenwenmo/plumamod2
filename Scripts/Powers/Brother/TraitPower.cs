using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Pluma.Scripts.Monsters;

namespace Pluma.Scripts;

// 特性：龙舌兰的核心成长属性。每层使龙舌兰造成的伤害提高1%，上限200层。
// 同时作为龙舌兰能力列表中唯一显示的能力：BrotherPower（解放者，代伤/同步/意图切换）
// 与 BrotherAttackTurnsPower（解放，剩余攻击回合）已通过 IsVisibleInternal=false 隐藏，
// 其机制说明整合进本能力的本地化描述中。
[RegisterPower]
public class TraitPower : ModPowerTemplate
{
    public const int MaxTrait = 200;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 供整合描述使用（仅显示用途，不参与任何逻辑计算）
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("TraitThreshold", Brother.TraitThreshold),
        new DynamicVar("AttackIntentTurns", Brother.ATTACK_INTENT_TURNS)
    ];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/TraitPower.png",
        BigIconPath: "res://pluma/images/powers/TraitPower.png"
    );

    // 每层提高1%伤害，加成对所有来源为龙舌兰的伤害生效
    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer == Owner && Amount > 0)
        {
            return 1m + (decimal)Amount / 100m;
        }
        return 1m;
    }

    // 层数变化后检测上限：若超过200，则减去超出部分
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not TraitPower || power.Owner != Owner) return;

        if (Amount > MaxTrait)
        {
            decimal excess = Amount - MaxTrait;
            await PowerCmd.Apply<TraitPower>(
                choiceContext,
                Owner,
                -excess,
                Owner,
                cardSource
            );
        }
    }
}