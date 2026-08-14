using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips; // 新增

namespace Pluma.Scripts;

// 黎博利Vigilance：2费罕见技能，对所有敌人施加2层创伤，获得所有敌人总创伤层数的格挡。升级后施加3层。
[RegisterCard(typeof(PlumaCardPool))]
public class LiberiVigilance : ModCardTemplate
{
    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    // 创伤层数动态变量（基础2，升级后+1）
    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        ModCardVars.Int("OpenWound", 2)
    };
    
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        MyKeywords.MuscleMemory,
    ];

    public LiberiVigilance() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int woundStacks = DynamicVars["OpenWound"].IntValue;
        var enemies = CombatState.HittableEnemies;

        // 1. 对所有敌人施加创伤
        foreach (var enemy in enemies)
        {
            await PowerCmd.Apply<OpenWoundPower>(
                choiceContext,
                enemy,
                woundStacks,
                base.Owner.Creature,
                this
            );
        }

        // 2. 计算所有敌人创伤层数总和
        int totalWound = 0;
        foreach (var enemy in enemies)
        {
            var power = enemy.Powers.OfType<OpenWoundPower>().FirstOrDefault();
            if (power != null)
                totalWound += (int)power.Amount;
        }

        // 3. 获得等同于总创伤层数的格挡
        if (totalWound > 0)
        {
            await CreatureCmd.GainBlock(
                base.Owner.Creature,
                totalWound,
                ValueProp.Unpowered,
                cardPlay
            );
        }
    }

    // 添加创伤能力的悬浮提示
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => new[]
    {
        HoverTipFactory.FromPower<OpenWoundPower>()
    };

    protected override void OnUpgrade()
    {
        DynamicVars["OpenWound"].UpgradeValueBy(1m); // 2 → 3
    }
}