using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace Pluma.Scripts;

/// <summary>
/// 「随机基酒 X」术语的统一实现。
/// 定义：获得 X 个不同的随机基酒。优先获得当前手牌中尚未拥有的基酒组；
/// 基酒组包含基酒本身及其对应的鸡尾酒——例如手牌中有金汤力，也视为已拥有金酒。
/// 如果当前手牌中已经拥有全部 6 组基酒，则忽略该限制，
/// 从 6 种基酒中随机选取不重复的基酒，相当于开始新的一组。
/// </summary>
public static class BaseSpiritGeneration
{
    /// <summary>六种基酒的模型类型（顺序固定，仅用于确定种类）。</summary>
    private static readonly IReadOnlyList<Type> BaseSpiritCardTypes = new[]
    {
        typeof(Gin),
        typeof(Tequila),
        typeof(Whiskey),
        typeof(Rum),
        typeof(Vodka),
        typeof(Brandy),
    };

    /// <summary>「随机基酒」术语的悬浮提示（标题与描述见 static_hover_tips 表）。</summary>
    public static IHoverTip RandomBaseSpiritHoverTip { get; } = new HoverTip(
        new LocString("static_hover_tips", "RANDOM_BASE_SPIRIT.title"),
        new LocString("static_hover_tips", "RANDOM_BASE_SPIRIT.description"));

    /// <summary>
    /// 生成 count 个不重复的随机基酒。
    /// </summary>
    /// <param name="player">获得基酒的玩家。</param>
    /// <param name="count">生成数量；超过 6 时按「完整一组 + 新的一组」规则循环。</param>
    /// <param name="combatState">当前战斗状态。</param>
    /// <param name="rng">局内确定性随机源（player.RunState.Rng.CombatCardGeneration），
    /// 保证多人各端生成结果一致，严禁 new Random()。</param>
    public static List<CardModel> GenerateRandomBaseSpirits(
        Player player, int count, ICombatState combatState, Rng rng)
    {
        var generated = new List<CardModel>();
        if (count <= 0 || combatState == null || player == null)
        {
            return generated;
        }

        var handSpiritTypes = GetHandBaseSpiritTypes(player);

        // 手牌已集齐全部 6 组基酒 → 忽略排除，从 6 种中重新随机选取（开始新的一组）。
        bool handHasAllSix = BaseSpiritCardTypes.All(handSpiritTypes.Contains);
        var available = (handHasAllSix
            ? BaseSpiritCardTypes
            : BaseSpiritCardTypes.Where(t => !handSpiritTypes.Contains(t))).ToList();

        // 已生成过的种类：优先保证整体不重复，集齐一组后才允许开新的一组。
        var generatedTypes = new HashSet<Type>();

        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0)
            {
                // 当前组已用完：先尝试从全部 6 种中排除本轮已生成过的种类。
                available = BaseSpiritCardTypes.Where(t => !generatedTypes.Contains(t)).ToList();
                if (available.Count == 0)
                {
                    // 已生成完整一组 6 种 → 开始新的一组（忽略限制）。
                    available = BaseSpiritCardTypes.ToList();
                }
            }

            int index = rng.NextInt(available.Count);
            Type spiritType = available[index];
            available.RemoveAt(index);
            generatedTypes.Add(spiritType);
            generated.Add(CreateSpirit(spiritType, combatState, player));
        }

        return generated;
    }

    /// <summary>
    /// 获取手牌中已拥有的基酒组（基酒 + 对应鸡尾酒）。
    /// 例如手牌中有金汤力（GinTonic），也会把金酒（Gin）加入已拥有集合。
    /// </summary>
    private static HashSet<Type> GetHandBaseSpiritTypes(Player player)
    {
        var handCards = PileType.Hand.GetPile(player).Cards;
        var spiritTypes = new HashSet<Type>();

        foreach (var card in handCards)
        {
            if (card is IBaseSpiritCard)
            {
                spiritTypes.Add(card.GetType());
            }
            else if (card is ICocktailCard)
            {
                var baseSpiritType = GetBaseSpiritTypeForCocktail(card.GetType());
                if (baseSpiritType != null)
                {
                    spiritTypes.Add(baseSpiritType);
                }
            }
        }

        return spiritTypes;
    }

    /// <summary>
    /// 返回鸡尾酒对应的基酒类型；如果不是鸡尾酒，返回 null。
    /// </summary>
    private static Type? GetBaseSpiritTypeForCocktail(Type cocktailType)
    {
        if (cocktailType == typeof(GinTonic)) return typeof(Gin);
        if (cocktailType == typeof(Margarita)) return typeof(Tequila);
        if (cocktailType == typeof(OldFashioned)) return typeof(Whiskey);
        if (cocktailType == typeof(Mojito)) return typeof(Rum);
        if (cocktailType == typeof(Cosmopolitan)) return typeof(Vodka);
        if (cocktailType == typeof(Sidecar)) return typeof(Brandy);
        return null;
    }

    private static CardModel CreateSpirit(Type spiritType, ICombatState combatState, Player player)
    {
        if (spiritType == typeof(Gin)) return combatState.CreateCard<Gin>(player);
        if (spiritType == typeof(Tequila)) return combatState.CreateCard<Tequila>(player);
        if (spiritType == typeof(Whiskey)) return combatState.CreateCard<Whiskey>(player);
        if (spiritType == typeof(Rum)) return combatState.CreateCard<Rum>(player);
        if (spiritType == typeof(Vodka)) return combatState.CreateCard<Vodka>(player);
        return combatState.CreateCard<Brandy>(player);
    }
}