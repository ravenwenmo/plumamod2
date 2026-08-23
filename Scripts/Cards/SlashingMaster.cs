using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 切割大师：1费技能牌，生成3张切割。升级后生成4张。
[RegisterCard(typeof(PlumaCardPool))]
public class SlashingMaster : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 关键词（消耗）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust, // 添加原版关键词
        MyKeywords.MuscleMemory 
    ];

    // 生成数量，升级后 +1
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new CardsVar(3) };

    public SlashingMaster() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = DynamicVars.Cards.IntValue;
        for (int i = 0; i < count; i++)
        {
            await Slashing.CreateInHand(base.Owner, base.CombatState);
            await Cmd.Wait(0.1f); // 添加生成间隔，模仿 BladeDance 的流畅感
        }
    }

    // 悬浮预览切割牌（正确重写方法）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromCard<Slashing>()
    };

    
    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m); // 3 → 4
    }
}