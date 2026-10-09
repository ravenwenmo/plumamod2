using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using Pluma.Scripts;
using Pluma.Scripts.Monsters;

namespace Pluma.Monsters;

public class BrotherBuffIntent : BuffIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString locString = new LocString("intents", "PLUMA_BROTHER_BUFF" + ".description");
        ICombatState? combatState = owner.CombatState;
        locString.Add("IsMultiplayer", combatState != null && combatState.RunState.Players.Count > 1);

        // 动态传入当前每回合特性获取总量
        if (owner.Monster is Brother brother)
        {
            locString.Add("TraitPerTurn", brother.GetCurrentTraitPerTurn());
        }

        return locString;
    }
}

public class BrotherAttackIntent : AttackIntent, IIntentExtraValueText
{
    private readonly int _repeat;
    private readonly Func<int>? _repeatCalc;
    private readonly bool _isAoe;

    // 龙舌兰本体引用：渲染"剩余N回合"时需要实时读取其身上的 BrotherAttackTurnsPower。
    // 意图对象每次切换意图时都会重建（见 Brother.GetAttackIntent），所以这里直接持有怪物实例是安全的。
    private readonly Brother? _brother;

    protected override LocString IntentLabelFormat => new LocString("intents", "FORMAT_DAMAGE_MULTI");

    // 根据是否拥有范围攻击能力，使用不同的本地化前缀
    protected override string IntentPrefix =>
        _isAoe ? "PLUMA_BROTHER_ATTACK_AOE" : "PLUMA_BROTHER_ATTACK_SINGLE";

    public override int Repeats => _repeatCalc?.Invoke() ?? _repeat;

    public BrotherAttackIntent(int damage, int repeat, bool isAoe = false, Brother? brother = null)
    {
        base.DamageCalc = () => damage;
        _repeat = repeat;
        _isAoe = isAoe;
        _brother = brother;
    }

    public BrotherAttackIntent(int damage, Func<int> repeatCalc, bool isAoe = false, Brother? brother = null)
    {
        base.DamageCalc = () => damage;
        _repeatCalc = repeatCalc;
        _isAoe = isAoe;
        _brother = brother;
    }

    // 与游戏 intents.json 里 FORMAT_DAMAGE_MULTI（"{Damage}[font_size=18]×{Repeat}[/font_size]"）
    // 的小字号保持一致：即 "12×1" 里 "×1" 的字号。数字不套标签，用意图数值标签的基础字号（与 "12" 相同）。
    private const int SmallFontSize = 18;

    // 在攻击意图的数值（"12×1"）后面紧跟显示攻击循环的剩余回合数（数据来自 BrotherAttackTurnsPower）。
    // 文本由 IntentExtraValueTextPatch 渲染成 %Value 的兄弟节点：字体/字号/描边照抄数值标签，
    // 并挂在同一个 %IntentHolder 下，因此与数值同步上下摆动。
    // 文本按 BBCode 解析，所以这里把汉字缩小一号、数字保持基础字号。
    // 回合数归零时返回 null 不再显示（此时攻击循环已打完最后一击，只剩"回合结束后切回强化循环"这一步）。
    public string? GetIntentExtraValueText()
    {
        int turns = _brother?.AttackTurnsRemaining ?? 0;
        if (turns <= 0)
        {
            return null;
        }

        return $"[font_size={SmallFontSize}]剩[/font_size]{turns}[font_size={SmallFontSize}]回合[/font_size]";
    }

    // 意图图标动画键必须存在于 IntentAnimData（合法键为 attack_1~attack_5、buff 等）。
    // 基类 AttackIntent.GetAnimation 会拼上被覆盖的 IntentPrefix，生成不存在的键，
    // 导致 NIntent.UpdateVisuals 抛 KeyNotFoundException（图标不显示/不刷新）。
    // 这里按与基类相同的总伤害分档返回合法键。
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        int totalDamage = GetTotalDamage(targets, owner);
        string tier = totalDamage < 5 ? "1" : totalDamage < 10 ? "2" : totalDamage < 20 ? "3" : totalDamage < 40 ? "4" : "5";
        return "attack_" + tier;
    }

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return GetDamage(targets, owner) * Repeats;
    }

    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        IEnumerable<Creature> enemies = owner.CombatState.GetOpponentsOf(owner);
        bool isAoe = _isAoe;

        string key = isAoe
            ? "PLUMA_BROTHER_ATTACK_AOE.description"
            : "PLUMA_BROTHER_ATTACK_SINGLE.description";

        LocString locString = new("intents", key);
        locString.Add("Damage", GetDamage(enemies, owner));
        locString.Add("Repeat", Repeats);
        return locString;
    }

    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        IEnumerable<Creature> enemies = owner.CombatState.GetOpponentsOf(owner);
        LocString intentLabelFormat = IntentLabelFormat;
        intentLabelFormat.Add("Damage", GetDamage(enemies, owner));
        intentLabelFormat.Add("Repeat", Repeats);
        return intentLabelFormat;
    }

    public int GetDamage(IEnumerable<Creature> targets, Creature owner)
    {
        decimal num = DamageCalc();

        Player? me = owner.PetOwner ?? LocalContext.GetMe(owner.CombatState);
        if (me == null || me.Creature == null || me.RunState == null)
        {
            return Math.Max(0, (int)num);
        }

        if (targets != null && targets.Count() == 1)
        {
            Creature mo = targets.First();
            num = Hook.ModifyDamage(
                me.RunState,
                me.Creature.CombatState,
                mo,
                owner,
                DamageCalc(),
                ValueProp.Move,
                null,
                null,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out IEnumerable<AbstractModel> _
            );
        }
        else
        {
            num = Hook.ModifyDamage(
                me.RunState,
                me.Creature.CombatState,
                null,
                owner,
                DamageCalc(),
                ValueProp.Move,
                null,
                null,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out IEnumerable<AbstractModel> _
            );
        }

        return Math.Max(0, (int)num);
    }
}