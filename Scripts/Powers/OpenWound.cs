using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Pluma.Scripts;

// 创伤：持有者造成伤害时，先受到等量于层数的伤害，然后层数-1。
[RegisterPower]
public class OpenWoundPower : ModPowerTemplate, IHealthBarForecastSource
{
    private bool _isApplying;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/OpenWoundPower.png",
        BigIconPath: "res://pluma/images/powers/OpenWoundPower.png"
    );


    // public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    // {
    //     // 只处理持有者发起的攻击，且不是创伤自身造成的伤害
    //     if (command.Attacker != base.Owner || _isApplying) return;

    //     // 多段攻击（如 3x3）是一个 AttackCommand 内部循环执行多段，
    //     // Results 中每个内层列表对应实际执行的一段攻击；
    //     // AOE 攻击每段只产生一个内层列表（多目标伤害都在同一个列表内）。
    //     // 因此按列表数触发：多段按段数触发，AOE 不会因命中多个玩家而重复触发。
    //     int hitCount = command.Results.Count();
    //     if (hitCount <= 0) return;

    //     await TriggerMultiple(choiceContext, hitCount);
    // }

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != base.Owner || base.Amount <= 0)
            return Enumerable.Empty<HealthBarForecastSegment>();

        Color lightRed = new Color(1f, 0.6f, 0.6f);   // 浅红（左侧，对应短段）
        Color darkRed = new Color(0.6f, 0.05f, 0.05f); // 深红（右侧，对应长段）

        // 敌人持有创伤时，合并所有触发源的递减预测。
        // 每个触发源都按"造成当前层数伤害，再减 1 层"结算，因此合并后
        // 整体为逐段递减序列；来源按触发先后排列：
        // 兜割预览（本回合出牌即触发）→ 假动作（敌方回合开始时触发）→ 攻击意图（敌方回合攻击时触发）。
        int helmBreakerHits = CardAimPreview.IsAimingWith<HelmBreaker>(base.Owner) ? HelmBreaker.TraumaTriggerCount : 0;
        int feintHits = GetFeintHitCount();
        int intentHits = GetEnemyAttackHitCount();
        int totalHits = helmBreakerHits + feintHits + intentHits;
        if (totalHits >= 1)
        {
            int amount = (int)base.Amount;
            int segmentCount = totalHits < amount ? totalHits : amount;
            var segments = new List<HealthBarForecastSegment>();
            int order = 0; // order 越小越靠近右边缘，逐段向左累加
            for (int i = 0; i < segmentCount; i++)
            {
                int segmentLength = amount - i;
                float factor = (segmentCount == 1) ? 0f : (float)i / (segmentCount - 1);
                // 右边缘（第一段、最长段）深红，越往左越浅，与原有风格一致
                Color color = new Color(
                    Mathf.Lerp(darkRed.R, lightRed.R, factor),
                    Mathf.Lerp(darkRed.G, lightRed.G, factor),
                    Mathf.Lerp(darkRed.B, lightRed.B, factor)
                );

                segments.Add(new HealthBarForecastSegment(
                    segmentLength,                              // 该段长度 = 层数 - i
                    color,
                    HealthBarForecastGrowthDirection.FromRight,
                    order,                                      // 排列顺序
                    null                                        // material
                ));
                order += segmentLength;
            }
            return segments;
        }

        // 玩家持有 / 无任何触发源：保持原有单段显示
        int count = (int)base.Amount;
        var fallbackSegments = new List<HealthBarForecastSegment>();
        for (int i = 0; i < count; i++)
        {
            float factor = (count == 1) ? 0f : (float)i / (count - 1);
            int order = count - 1 - i;
            Color color = new Color(
                Mathf.Lerp(lightRed.R, darkRed.R, factor),
                Mathf.Lerp(lightRed.G, darkRed.G, factor),
                Mathf.Lerp(lightRed.B, darkRed.B, factor)
            );

            fallbackSegments.Add(new HealthBarForecastSegment(
                1,                                          // 长度 1
                color,
                HealthBarForecastGrowthDirection.FromRight,
                order,                                      // 排列顺序
                null                                        // material
            ));
        }

        return fallbackSegments;
    }
    //效果意外还行的渐变颜色条，备选

    /*
    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != base.Owner || base.Amount <= 0)
            return Enumerable.Empty<HealthBarForecastSegment>();

        var segments = new List<IEnumerable<HealthBarForecastSegment>>();

        Color lightRed = new Color(1f, 0.6f, 0.6f);   // 浅红（左侧长段）
        Color darkRed  = new Color(0.6f, 0.05f, 0.05f); // 深红（右侧短段）

        // i 从 Amount 递减到 1：i 大 = 长段 = 浅色，i 小 = 短段 = 深色
        for (int i = (int)base.Amount; i > 0; i--)
        {
            // i 越大颜色越浅，i 越小颜色越深
            float factor = (float)i / (float)base.Amount; // 1.0（浅）→ 接近0（深）
            Color segmentColor = new Color(
                Mathf.Lerp(darkRed.R, lightRed.R, factor),
                Mathf.Lerp(darkRed.G, lightRed.G, factor),
                Mathf.Lerp(darkRed.B, lightRed.B, factor)
            );

            segments.Add(HealthBarForecasts.Single(
                i,                                      // 长度递减：4,3,2,1
                segmentColor,
                HealthBarForecastGrowthDirection.FromRight
            ));
        }

        return segments.SelectMany(s => s);
    }
    */
    //我Chovy败给你了不要了

    /// <summary>
    /// 获取持有者下一次攻击意图的总段数（多个攻击意图的段数之和）。
    /// 持有者不是敌人（玩家）或当前没有攻击意图时返回 0。
    /// </summary>
    private int GetEnemyAttackHitCount()
    {
        MonsterModel? monster = base.Owner?.Monster;
        if (monster == null) return 0;

        int hits = 0;
        foreach (AbstractIntent intent in monster.NextMove.Intents)
        {
            if (intent is AttackIntent attackIntent)
                hits += attackIntent.Repeats;
        }
        return hits;
    }

    /// <summary>
    /// 获取持有者身上假动作（FeintPower）的总层数。
    /// 只有敌人身上的假动作会触发创伤（敌方回合开始时），
    /// 持有者是玩家或没有假动作时返回 0。
    /// </summary>
    private int GetFeintHitCount()
    {
        if (base.Owner?.IsMonster != true) return 0;
        return base.Owner.Powers.OfType<FeintPower>().Sum(power => (int)power.Amount);
    }

    [HarmonyPatch(
        typeof(CreatureCmd),
        nameof(CreatureCmd.Damage),
        [
            typeof(PlayerChoiceContext),
            typeof(IEnumerable<Creature>),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel),
            typeof(CardPlay)
        ]
    )]
    public class OpenWoundPower_CreatureCmd_Damage_Patch
    {
        private static async Task TriggerWound(PlayerChoiceContext choiceContext, Creature? dealer)
        {
            if (dealer == null) return;
            // 战斗结束/结算中不再触发，避免 Cmd.Wait 因战斗拆除被取消而抛异常
            if (dealer.CombatState?.IsLiveCombat() != true) return;
            if (dealer.HasPower<OpenWoundPower>())
            {
                OpenWoundPower woundPower = dealer.GetPower<OpenWoundPower>();
                if (woundPower != null && !woundPower._isApplying)
                {
                    await woundPower.TriggerMultiple(choiceContext, 1);
                }
            }
        }

        [HarmonyPostfix]
        private static async Task<IEnumerable<DamageResult>> Postfix(
            Task<IEnumerable<DamageResult>> __result,
            PlayerChoiceContext choiceContext,
            Creature? dealer)
        {
            // 等待原本的 Damage 完整执行（原任务异常照常传播，保证多人状态一致）
            IEnumerable<DamageResult> results = await __result;

            // 创伤触发是附加效果：任何异常只记录并忽略，
            // 绝不能让它故障化 Damage 任务——调用方是敌方回合链，
            // 异常会杀死整个回合循环导致客户端卡死（见多人卡死根因报告）。
            try
            {
                await TriggerWound(choiceContext, dealer);
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"创伤触发失败（已忽略，避免打断回合流程）: dealer={dealer?.Name} ex={ex}");
            }

            // 保留原始 Damage 返回值
            return results;
        }
    }

    /// <summary>
    /// 连续触发多次创伤效果，伤害来源与正常创伤一致（无攻击者、无卡牌）。
    /// </summary>
    public async Task TriggerMultiple(PlayerChoiceContext choiceContext, int times)
    {
        for (int i = 0; i < times; i++)
        {
            if (this.Amount <= 0 || this.Owner == null) break;
            if (_isApplying) break;
            _isApplying = true;
            if (i == 0) { await Cmd.Wait(0.2f); }
            try
            {
                // 创伤翻倍标记（居合）：本次伤害 ×(1+层数)，造成后清空标记确保只有一次增伤
                int amplify = base.Owner.GetPowerAmount<WoundAmplifyPower>();
                decimal damage = base.Amount;
                if (amplify > 0)
                {
                    damage *= 1m + amplify;
                }

                // 伤害来源设为持有者自身（与原版 ConstrictPower 等自伤能力惯例一致）：
                // 原版部分能力（如 LeadershipPower）在 ModifyDamage 中先解引用 dealer 再检查
                // Unpowered，dealer 传 null 会 NRE，异常沿 Damage 任务传播会杀死整个敌方
                // 回合循环导致多人客户端卡死。
                await CreatureCmd.Damage(
                    choiceContext,
                    base.Owner,
                    damage,
                    ValueProp.Unpowered | ValueProp.Unblockable,
                    base.Owner
                );
                if (amplify > 0)
                {
                    await PowerCmd.Remove<WoundAmplifyPower>(base.Owner);
                }
                await PowerCmd.Decrement(this);
            }
            finally
            {
                _isApplying = false;
            }
            //await Cmd.Wait(0.01f);
        }
    }
}