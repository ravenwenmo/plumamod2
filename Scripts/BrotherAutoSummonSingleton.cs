using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using Pluma.Scripts.Commands;
using Pluma.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace Pluma.Scripts;

// 进入战斗时自动召唤龙舌兰，条件二选一：
// 1. 本局已召唤过（HasBeenSummoned == true）：每场战斗开始自动出现，不再检查卡组条件；
// 2. 首次召唤：玩家卡组中存在任意实现 IBaseSpiritRelatedCard 的牌
//    （兜底：存在调酒师的小壶 BartendersFlask）。
// 龙舌兰按持久化状态恢复力量、意图、剩余攻击回合与生命值；
// 若上一场战斗中龙舌兰死亡或从未被召唤，则按默认状态召唤。
[RegisterSingleton]
public class BrotherAutoSummonSingleton : HookedSingletonModel
{
    // 注意：必须订阅 Run 钩子而不是 Combat 钩子！
    // 游戏源码中 Hook.AfterRoomEntered 的派发为 runState.IterateHookListeners(null)，
    // 只遍历局内（run）级钩子模型；HookType.Combat 订阅的模型挂在战斗级钩子列表上，
    // 该钩子永远不会被派发到（此前多次实测进入战斗无任何日志输出）。
    // Hook.AfterCombatEnd 之所以能到达 Combat 钩子模型，
    // 是因为它的派发是 runState.IterateHookListeners(combatState)，包含战斗级子列表。
    public BrotherAutoSummonSingleton() : base(HookType.Run)
    {
        GD.Print("[BrotherAutoSummon] 构造函数被调用了");  // 👈 位置 1
    }

    // 进入战斗时自动召唤（战斗正式开始前一刻触发）。
    // 注意两点：
    // 1. 必须订阅 Run 钩子而不是 Combat 钩子：Hook.AfterRoomEntered 的派发为
    //    runState.IterateHookListeners(null)，不会派发给战斗级钩子模型；
    // 2. 必须用 BeforeCombatStart 而不是 AfterRoomEntered：AfterRoomEntered 触发时
    //    CombatManager.IsInProgress 仍为 false，CreatureCmd.Add 会抛
    //    "Attempted to add a creature outside of combat" 导致房间进入流程卡死；
    //    BeforeCombatStart 在 CombatManager 中于 IsInProgress=true 之后才派发
    //    （CombatManager.cs:592-594），此时 AddPet 可用。
    public override async Task BeforeCombatStart()
    {
        GD.Print("[BrotherAutoSummon] BeforeCombatStart 被触发了！");  // 👈 位置 2
        if (CurrentRunState == null)
        {
            GD.Print("[BrotherAutoSummon] CurrentRunState 为空，跳过");
            return;
        }

        IEnumerable<Player> players = CurrentRunState.Players;
        GD.Print($"[BrotherAutoSummon] 找到玩家数量: {players.Count()}");  // 👈 位置 3
        foreach (Player player in players)
        {
            GD.Print($"[BrotherAutoSummon] 检查玩家: {player}");  // 👈 位置 4
            if (player.IsBrotherAlive())
            {
                GD.Print($"[BrotherAutoSummon] 跳过 {player}：Brother 已存活");
                GD.Print($"[BrotherAutoSummon] Skip {player}: Brother already alive");
                continue;
            }

            BrotherStateData state = Entry.BrotherStateData.Get(player);
            GD.Print($"[BrotherAutoSummon] 玩家 {player}，HasBeenSummoned={state.HasBeenSummoned}");  // 👈 位置 5

            if (state.HasBeenSummoned)
            {
                // 本局已召唤过：每场战斗开始自动出现，跳过卡组检测
                GD.Print($"[BrotherAutoSummon] 召唤 {player}：已召唤过");
                GD.Print($"[BrotherAutoSummon] Summon for {player}: HasBeenSummoned=true (summoned before)");
                await BrotherCmd.AutoSummon(player);
                continue;
            }

            // 首次召唤需满足卡组条件 —— 已注释：让龙舌兰每场战斗必定出现，不再检查卡组条件
            // if (!DeckCanSummonBrother(player))
            // {
            //     GD.Print($"[BrotherAutoSummon] 跳过 {player}：首次召唤但卡组条件不满足");
            //     GD.Print($"[BrotherAutoSummon] Skip {player}: HasBeenSummoned=false and deck has no base-spirit/cocktail related cards");
            //     continue;
            // }
            GD.Print($"[BrotherAutoSummon] 召唤 {player}：卡组条件已注释，每场战斗必定召唤");
            GD.Print($"[BrotherAutoSummon] Summon for {player}: deck condition commented out, always summon");
            await BrotherCmd.AutoSummon(player);
        }
    }

    // 战斗结束时以生物实时数据为准快照持久化状态（力量等高频字段），
    // 防止存档框架在房间结算期间用旧快照覆盖，导致下一场战斗继承错误。
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        GD.Print("[BrotherAutoSummon] AfterCombatEnd 被触发了！");
        foreach (Player player in room.CombatState.Players)
        {
            Creature? brother = player.PlayerCombatState?.GetPet<Monsters.Brother>();
            if (brother == null || !brother.IsAlive)
            {
                continue;
            }

            int strength = brother.GetPowerAmount<StrengthPower>();
            Entry.BrotherStateData.Modify(player, s => s.Strength = strength);
            //BrotherStateData.SyncStrength(player, strength);
            GD.Print($"[BrotherAutoSummon] Combat end snapshot for {player}: strength={strength}");
        }
    }

    // 卡组中存在任意实现 IBaseSpiritRelatedCard 的牌（悬浮提示或关键词展示基酒/鸡尾酒），
    // 或存在调酒师的小壶（BartendersFlask 兜底），则自动召唤。
    // 注意：部分牌（如调酒师的小壶）仅在悬浮提示中展示基酒关键词，
    // 自身 Keywords 不含该关键词，不能依赖 card.Keywords 判断。

    private static bool DeckCanSummonBrother(Player player)
    {
        var cards = player.Deck.Cards;
        bool hasRelatedCard = cards.Any(card => card is IBaseSpiritRelatedCard);
        bool hasFlask = cards.Any(card => card is BartendersFlask);
        GD.Print($"[BrotherAutoSummon] 卡组扫描: 总卡牌={cards.Count()}, 基酒相关={hasRelatedCard}, 小壶={hasFlask}"); 
        return hasRelatedCard || hasFlask;
    }
}