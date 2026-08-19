using System.Linq;
using System.Runtime.CompilerServices;
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
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Rooms; // 提供 RestSiteOption

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

    private bool _hasObtainedBrotherRelic = false;
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner.GetRelic<BrotherRelic>() == null && !_hasObtainedBrotherRelic)
        {
            await RelicCmd.Obtain<BrotherRelic>(Owner);
            _hasObtainedBrotherRelic = true;
        }
    }
    
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

        // 只要目标不是自己（避免自伤回血），就回血
        if (target == base.Owner.Creature) return;

        Flash();
        await CreatureCmd.Heal(base.Owner.Creature, 1);
    }

    private bool ban_heal = false;
    // 移除“休息”选项，让玩家无法选择
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != base.Owner) return false;

        var healOption = options.FirstOrDefault(o => o is HealRestSiteOption);
        if (healOption == null) return false;

        // 检查是否有除休息以外的可用选项
        bool hasOtherEnabledOption = options.Any(o => o != healOption && o.IsEnabled);
        // 我焯为什么只能检测到敲牌是否可用啊不应该啊md算了就这样吧
        
        if (hasOtherEnabledOption)
        {
            options.Remove(healOption);
            ban_heal = false;
            Flash();
            return true;
        }
        ban_heal = true;
        return false;
    }
    
    // 让“休息”回血变为 0，选项保留，避免卡死
    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal originalAmount)
    {
        if (creature == base.Owner.Creature && ban_heal)
        {
            Flash();
            return 0;
        }
        return originalAmount;
    }
    
}