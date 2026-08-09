using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.RestSite; // 提供 RestSiteOption

namespace Pluma.Scripts;

[RegisterRelic(typeof(PlumaRelicPool))]
[RegisterCharacterStarterRelic(typeof(PlumaCharacter))]
[RegisterTouchOfOrobasRefinement(typeof(AquaDawn))]
public class ReaperBadge : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new CardsVar(1) };

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://pluma/images/relics/{GetType().Name}.png",
        IconOutlinePath: $"res://pluma/images/relics/{GetType().Name}.png",
        BigIconPath: $"res://pluma/images/relics/{GetType().Name}.png"
    );

    // 造成伤害时回血
    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        // 必须是遗物持有者自己造成的伤害
        if (dealer != base.Owner.Creature) return;

        // 只对敌人造成的伤害生效（排除自伤、反伤等）
        if (!base.Owner.Creature.CombatState.HittableEnemies.Contains(target)) return;

        Flash();                              // 回血时闪光
        await CreatureCmd.Heal(base.Owner.Creature, 1); // 回复等于遗物层数？这里遗物没有层数，固定回复1点。
    }
    // 移除“休息”选项，让玩家无法选择
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != base.Owner) return false;

        // 假设 RestSiteOption 有一个 Type 属性，其值为 RestSiteOptionType.Heal
        var healOption = options.FirstOrDefault(o => o.GetType() == typeof(HealRestSiteOption));
        if (healOption != null)
        {
            options.Remove(healOption);
            Flash();
            return true;
        }
        return false;
    }
}