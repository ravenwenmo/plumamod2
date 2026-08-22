using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Models;

namespace Pluma.Scripts.Cards;

// ALL! IN!：稀有X费技能牌。获得X张基酒：先获得若干组完整的6种基酒，剩余部分获得随机不重复基酒。升级后数量+1。
[RegisterCard(typeof(PlumaCardPool))]
public class AllIn : ModCardTemplate
{
    private const int energyCost = 0; // X费牌，实际费用由 HasEnergyCostX 控制
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    protected override bool HasEnergyCostX => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://pluma/images/cards/{GetType().Name}.png"
    );

    public AllIn()
        : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();
        int amount = base.IsUpgraded ? x + 1 : x;
        if (amount <= 0) return;

        var player = base.Owner;
        var generated = new List<CardModel>();

        int fullGroups = amount / 6;          // 完整组数
        int remainder = amount % 6;           // 剩余随机不重复数量

        // 生成 n 组完整的 6 种基酒
        for (int group = 0; group < fullGroups; group++)
        {
            generated.Add(base.CombatState.CreateCard<Gin>(player));
            generated.Add(base.CombatState.CreateCard<Tequila>(player));
            generated.Add(base.CombatState.CreateCard<Whiskey>(player));
            generated.Add(base.CombatState.CreateCard<Rum>(player));
            generated.Add(base.CombatState.CreateCard<Vodka>(player));
            generated.Add(base.CombatState.CreateCard<Brandy>(player));
        }

        // 生成剩余 r 张不重复随机基酒
        if (remainder > 0)
        {
            var available = new List<CardModel>
            {
                base.CombatState.CreateCard<Gin>(player),
                base.CombatState.CreateCard<Tequila>(player),
                base.CombatState.CreateCard<Whiskey>(player),
                base.CombatState.CreateCard<Rum>(player),
                base.CombatState.CreateCard<Vodka>(player),
                base.CombatState.CreateCard<Brandy>(player),
            };

            var rng = base.Owner.RunState.Rng.CombatCardGeneration;
            for (int i = 0; i < remainder; i++)
            {
                int index = rng.NextInt(available.Count);
                generated.Add(available[index]);
                available.RemoveAt(index);
            }
        }

        if (generated.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, player);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级效果已在 OnPlay 中通过 base.IsUpgraded 判断，无需额外代码
    }
}