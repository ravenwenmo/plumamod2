using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;
using MegaCrit.Sts2.Core.Rooms; 


namespace Pluma.Scripts;

[RegisterSingleton]
public class SlashingComboSingleton : HookedSingletonModel
{
    // 持有自身的静态实例，供外部静态调用
    public static SlashingComboSingleton Instance { get; private set; }

    // 为每个玩家维护独立的连击计数器
    // 注意：0.113 起 RitsuLib 的单例是「每局一个克隆实例」，实例由 MemberwiseClone 生成、
    // 构造函数不会执行，所以这个字段不能是 readonly，且必须在 DeepCloneFields 里重建，
    // 否则所有实例会共用同一份字典（状态跨局泄漏）。
    private Dictionary<Player, int> _comboCounters = new();

    public SlashingComboSingleton() : base(HookType.Combat)
    {
        // 规范实例（canonical）构造时先把静态引用指过来；每局的真正实例见 DeepCloneFields
        Instance = this;
    }

    // 每局的克隆实例创建时：重建引用字段，并把静态 Instance 指向当前局的实例
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _comboCounters = new Dictionary<Player, int>();
        Instance = this;
    }

    // ===== 计数逻辑：卡牌打出时更新 =====
    
    
    // 进入战斗房间时清空所有连击计数
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _comboCounters.Clear();
        }
        return Task.CompletedTask;
    }
    //两个都加还能清不干净你？？？
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _comboCounters.Clear();
        return Task.CompletedTask;
    }
    
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        if (player == null) return Task.CompletedTask;

        if (cardPlay.Card.Keywords.Contains(MyKeywords.Slashing))
        {
            _comboCounters.TryGetValue(player, out int current);

            // 连击层数上限为 5，达到上限后不再增加
            if (current < 5)
            {
                _comboCounters[player] = current + 1;
            }
        }
        else
        {
            // 拥有利刃形态时，非切割牌不会清零计数器
            if (!player.Creature.HasPower<PrecisionRepetitionPower>())
            {
                _comboCounters.Remove(player);
            }
        }

        return Task.CompletedTask;
    }

    // ===== 施伤逻辑：造成伤害时附加创伤 =====
    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource == null || !cardSource.Keywords.Contains(MyKeywords.Slashing))
            return;

        var player = cardSource.Owner;
        if (player == null) return;
        
        // 只有 powered 攻击才会附加创伤；unpowered 伤害直接跳过
        if (!props.IsPoweredAttack())
            return;
        
        if (!_comboCounters.TryGetValue(player, out int count) || count <= 0)
            return;

        await PowerCmd.Apply<OpenWoundPower>(
            choiceContext,
            target,
            (decimal)count,
            dealer,
            cardSource
        );
    }

    // 实例方法：获取指定玩家的连击数
    public int GetComboCount(Player player)
    {
        return _comboCounters.TryGetValue(player, out int count) ? count : 0;
    }

    // 静态方法：供发光规则等外部调用
    public static int GetPlayerComboCount(Player? player)
    {
        if (player == null) return 0;
        return Instance?.GetComboCount(player) ?? 0;
    }
    // 实例方法：设置指定玩家的连击计数（例如“回旋切割”直接设为上限5）
    public void SetComboCount(Player player, int value)
    {
        if (player == null) return;
        if (value <= 0)
        {
            _comboCounters.Remove(player);
        }
        else
        {
            // 限制上限，避免超过5
            _comboCounters[player] = Math.Min(value, 5);
        }
    }
}