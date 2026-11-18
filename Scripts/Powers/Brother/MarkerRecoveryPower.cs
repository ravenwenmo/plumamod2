using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Players;


namespace Pluma.Scripts;

// 标记回收：每当能力持有者（玩家或其召唤物）的主人消耗基酒牌或辅料牌（MixerPack）时，
// 能力持有者获得等于该能力层数的特性。每当鸡尾酒牌生成时，能力持有者获得 (层数 ÷ 25) 层额外攻击段数。
//
// 层数规则：龙舌兰入场时由 Brother.AfterAddedToRoom 施加 InitialAmount(10) 层；
// 每打出一张同名能力牌（MarkerRecovery，+25）叠加 AmountPerCard(25) 层。
// 初始 10 层时 10 ÷ 25 = 0，喝鸡尾酒并不会真的拿到额外攻击段数，
// 所以层数 ≤ 25 的描述里不显示那一段（见 SmartDescriptionLocKey）。
[RegisterPower]
public class MarkerRecoveryPower : ModPowerTemplate
{
    // 初始层数（龙舌兰入场时施加）
    public const int InitialAmount = 10;

    // 每张同名能力牌叠加的层数（MarkerRecovery 的 MarkerRecoveryPower 变量）
    public const int AmountPerCard = 25;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 生成鸡尾酒牌时给出的额外攻击段数 = 层数 ÷ 25（向下取整，与下面 AfterCardGeneratedForCombat 一致）
    public static int ExtraHitsFor(int amount) => amount / AmountPerCard;

    // 层数大于 25（即至少打出过一张同名能力牌）才展示"生成鸡尾酒获得额外攻击段数"那段文本
    public bool ShowsExtraHits => Amount > AmountPerCard;

    // 描述里 {ExtraHits} 需要跟着层数实时变化，所以用一个取值时现算的派生变量
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DerivedIntVar("ExtraHits", () => ExtraHitsFor(Amount))
    };

    // 按层数在两套描述之间切换：初始 10 层只说特性，>25 层才补上多段斩那一段
    // 0.113 的 DynamicVar 只读 PreviewValue（非虚属性），不能再靠重写取值方法现算，
    // 所以在这里（每次显示悬浮提示都会走）顺手把 {ExtraHits} 刷新到最新层数。
    protected override string SmartDescriptionLocKey
    {
        get
        {
            RefreshExtraHits();
            return ShowsExtraHits
                ? base.SmartDescriptionLocKey
                : base.SmartDescriptionLocKey + "NoExtraHits";
        }
    }

    // 把派生变量的数值同步到当前层数
    private void RefreshExtraHits()
    {
        if (DynamicVars.TryGetValue("ExtraHits", out DynamicVar? extraHitsVar)
            && extraHitsVar is DerivedIntVar derivedVar)
        {
            derivedVar.Refresh();
        }
    }

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/MarkerRecoveryPower.png",
        BigIconPath: "res://pluma/images/powers/MarkerRecoveryPower.png"
    );

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        // 能力持有者可能是玩家，也可能是召唤物。
        // 召唤物监听其主人；玩家监听自身。
        Player? effectiveOwner = Owner.PetOwner ?? Owner.Player;
        if (effectiveOwner == null || card.Owner != effectiveOwner)
        {
            return;
        }

        // 基酒牌或辅料牌
        if (card is IBaseSpiritCard || card is MixerPack)
        {
            await PowerCmd.Apply<TraitPower>(
                choiceContext,
                Owner,
                (decimal)Amount,          // 特性层数 = 当前能力层数
                Owner,
                card
            );
        }
    }

    public override async Task AfterCardGeneratedForCombat(
        CardModel card,
        Player? creator)
    {
        if (card is not ICocktailCard)
        {
            return;
        }

        // 能力持有者可能是玩家，也可能是召唤物。
        Player? effectiveOwner = Owner.PetOwner ?? Owner.Player;
        if (effectiveOwner == null || card.Owner != effectiveOwner)
        {
            return;
        }

        // 额外攻击段数按整数算：初始 10 层时 10/25 = 0，就不该施加能力
        //（旧写法用 10m/25 = 0.4 判断 > 0，会真的给龙舌兰挂上一个 0 层的多段斩，图标上显示 0）
        int extraHits = ExtraHitsFor(Amount);
        if (extraHits > 0)
        {
            await PowerCmd.Apply<BrotherExtraHitsPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                extraHits,
                null,
                card
            );
        }
    }

    // 只用于描述显示：数值由 Refresh() 现算并写回 BaseValue/PreviewValue。
    // 0.113 的 DynamicVar 删掉了 GetBaseValueForIConvertible，显示一律走 PreviewValue，
    // 因此这里改成「读的时候刷新」而不是「重写取值方法」。
    private sealed class DerivedIntVar : DynamicVar
    {
        private readonly Func<int> _value;

        public DerivedIntVar(string name, Func<int> value)
            : base(name, 0m)
        {
            _value = value;
        }

        // BaseValue 的 setter 会顺带 ResetToBase()，把 PreviewValue 一起同步
        public void Refresh() => BaseValue = _value();

        public override void UpdateCardPreview(
            CardModel card,
            CardPreviewMode previewMode,
            Creature? target,
            bool runGlobalHooks)
        {
            Refresh();
        }

        public override string ToString() => _value().ToString();
    }
}