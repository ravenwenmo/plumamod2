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

namespace Pluma.Monsters;

public class BrotherBuffIntent : BuffIntent
{
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        LocString locString = new LocString("intents", "PLUMA_BROTHER_BUFF" + ".description");
        ICombatState? combatState = owner.CombatState;
        locString.Add("IsMultiplayer", combatState != null && combatState.RunState.Players.Count > 1);
        return locString;
    }
}

public class BrotherAttackIntent : AttackIntent
{
    private readonly int _repeat;
    private readonly Func<int>? _repeatCalc;
    private readonly bool _isAoe;

    protected override LocString IntentLabelFormat => new LocString("intents", "FORMAT_DAMAGE_MULTI");

    // 根据是否拥有范围攻击能力，使用不同的本地化前缀
    protected override string IntentPrefix =>
        _isAoe ? "PLUMA_BROTHER_ATTACK_AOE" : "PLUMA_BROTHER_ATTACK_SINGLE";

    public override int Repeats => _repeatCalc?.Invoke() ?? _repeat;

    public BrotherAttackIntent(int damage, int repeat, bool isAoe = false)
    {
        base.DamageCalc = () => damage;
        _repeat = repeat;
        _isAoe = isAoe;
    }

    public BrotherAttackIntent(int damage, Func<int> repeatCalc, bool isAoe = false)
    {
        base.DamageCalc = () => damage;
        _repeatCalc = repeatCalc;
        _isAoe = isAoe;
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