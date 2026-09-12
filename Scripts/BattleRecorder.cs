using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Pluma.Scripts.CombatSolverAdapter;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace Pluma.Scripts;

/// <summary>
/// 战斗数据记录器：为 DefectGo 抓牌 AI 收集奖励信号。
///
/// 进入 CombatRoom 时记录战前快照（血量/牌组/遗物/药水/seed/楼层），
/// 战斗结束时补记战后快照与胜负，以 JSONL（每场一行）追加写入
/// user://logs/pluma/pluma_battles.jsonl（含 schema_version，供 DefectGo 按版本解析）。
///
/// 记录失败只记日志，绝不影响战斗本身。所有日志带 [鸡煲] 前缀。
/// </summary>
[RegisterSingleton]
public class BattleRecorder : HookedSingletonModel
{
    private BattleSnapshot? _pending;

    public BattleRecorder() : base(HookType.Combat)
    {
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom)
        {
            try
            {
                _pending = CaptureStartSnapshot(combatRoom);
            }
            catch (Exception e)
            {
                _pending = null;
                Entry.Logger.Warn($"[鸡煲] 战前快照捕获失败（跳过本场记录）: {e}");
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (_pending is { } start)
        {
            try
            {
                PlumaBattleRecord record = FinalizeRecord(room, start);
                AppendRecord(record);
                Entry.Logger.Info(
                    $"[鸡煲] 战斗数据已记录: seed={record.seed_string} 楼层={record.total_floor} " +
                    $"遭遇={record.encounter_id} HP {record.hp_before}->{record.hp_after} " +
                    $"结果={record.outcome} 回合={record.turns_taken} 求解器接管={record.combat_solver_active}");
            }
            catch (Exception e)
            {
                Entry.Logger.Warn($"[鸡煲] 战后数据记录失败（不影响战斗）: {e}");
            }
            finally
            {
                _pending = null;
            }
        }

        return Task.CompletedTask;
    }

    private static BattleSnapshot CaptureStartSnapshot(CombatRoom room)
    {
        var combat = room.CombatState
            ?? throw new InvalidOperationException("CombatRoom 没有 CombatState");
        if (combat.Players.Count != 1)
        {
            throw new InvalidOperationException($"暂只记录单人战斗（当前 {combat.Players.Count} 人）");
        }

        Player player = combat.Players[0];
        IRunState? run = combat.RunState;

        return new BattleSnapshot
        {
            timestamp_unix_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            seed_string = run?.Rng.StringSeed ?? "",
            seed_ulong = run?.Rng.Seed ?? 0,
            act_index = run?.CurrentActIndex ?? -1,
            act_floor = run?.ActFloor ?? -1,
            total_floor = run?.TotalFloor ?? -1,
            encounter_id = room.Encounter?.Id.Entry ?? "",
            hp_before = player.Creature?.CurrentHp ?? -1,
            max_hp_before = player.Creature?.MaxHp ?? -1,
            deck_before = SerializeDeck(player),
            relics_before = player.Relics.Select(r => r.Id.Entry).ToList(),
            potions_before = player.Potions.Select(p => p.Id.Entry).ToList(),
        };
    }

    private static PlumaBattleRecord FinalizeRecord(CombatRoom room, BattleSnapshot start)
    {
        var combat = room.CombatState
            ?? throw new InvalidOperationException("CombatRoom 没有 CombatState");
        Player player = combat.Players.FirstOrDefault()
            ?? throw new InvalidOperationException("战斗结束时找不到玩家");

        bool playerDead = player.Creature == null || !player.Creature.IsAlive;
        bool allEnemiesGone = combat.Enemies.All(e => !e.IsAlive);
        string outcome = playerDead ? "defeat" : allEnemiesGone ? "victory" : "other";

        return new PlumaBattleRecord
        {
            schema_version = 1,
            timestamp_unix_ms = start.timestamp_unix_ms,
            character_id = Entry.ModId,
            seed_string = start.seed_string,
            seed_ulong = start.seed_ulong,
            act_index = start.act_index,
            act_floor = start.act_floor,
            total_floor = start.total_floor,
            encounter_id = start.encounter_id,
            hp_before = start.hp_before,
            max_hp_before = start.max_hp_before,
            deck_before = start.deck_before,
            relics_before = start.relics_before,
            potions_before = start.potions_before,
            hp_after = player.Creature?.CurrentHp ?? 0,
            max_hp_after = player.Creature?.MaxHp ?? -1,
            deck_after = SerializeDeck(player),
            relics_after = player.Relics.Select(r => r.Id.Entry).ToList(),
            potions_after = player.Potions.Select(p => p.Id.Entry).ToList(),
            outcome = outcome,
            turns_taken = player.PlayerCombatState?.TurnNumber ?? -1,
            combat_solver_active = CombatSolverBridgeStatus.IsActive,
        };
    }

    private static List<string> SerializeDeck(Player player)
    {
        // 每张牌记 "CardId|升级等级"，牌组顺序保持当前牌堆顺序。
        return player.Deck.Cards
            .Select(c => $"{c.Id.Entry}|{c.CurrentUpgradeLevel}")
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private static void AppendRecord(PlumaBattleRecord record)
    {
        string dir = Path.Combine(OS.GetUserDataDir(), "logs", "pluma");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "pluma_battles.jsonl");
        File.AppendAllText(path, JsonSerializer.Serialize(record, JsonOptions) + "\n");
    }

    private sealed class BattleSnapshot
    {
        public long timestamp_unix_ms { get; set; }
        public string seed_string { get; set; } = "";
        public ulong seed_ulong { get; set; }
        public int act_index { get; set; }
        public int act_floor { get; set; }
        public int total_floor { get; set; }
        public string encounter_id { get; set; } = "";
        public int hp_before { get; set; }
        public int max_hp_before { get; set; }
        public List<string> deck_before { get; set; } = new();
        public List<string> relics_before { get; set; } = new();
        public List<string> potions_before { get; set; } = new();
    }

    /// <summary>每场战斗一条的 JSONL 记录（schema_version = 1，DefectGo 按此解析）。</summary>
    internal sealed class PlumaBattleRecord
    {
        public int schema_version { get; set; }
        public long timestamp_unix_ms { get; set; }
        public string character_id { get; set; } = "";
        public string seed_string { get; set; } = "";
        public ulong seed_ulong { get; set; }
        public int act_index { get; set; }
        public int act_floor { get; set; }
        public int total_floor { get; set; }
        public string encounter_id { get; set; } = "";
        public int hp_before { get; set; }
        public int max_hp_before { get; set; }
        public List<string> deck_before { get; set; } = new();
        public List<string> relics_before { get; set; } = new();
        public List<string> potions_before { get; set; } = new();
        public int hp_after { get; set; }
        public int max_hp_after { get; set; }
        public List<string> deck_after { get; set; } = new();
        public List<string> relics_after { get; set; } = new();
        public List<string> potions_after { get; set; } = new();
        public string outcome { get; set; } = "";
        public int turns_taken { get; set; }
        public bool combat_solver_active { get; set; }
    }
}
