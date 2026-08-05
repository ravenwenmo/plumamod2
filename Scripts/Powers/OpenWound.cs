using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

// 创伤：持有者造成伤害时，先受到等量于层数的伤害，然后层数-1。
[RegisterPower]
public class OpenWoundPower : ModPowerTemplate, IHealthBarForecastSource
{
    private bool _isApplying;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://pluma/images/powers/OpenWound.png",
        BigIconPath: "res://pluma/images/powers/OpenWound.png"
    );

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props,
        Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner || _isApplying) return;

        _isApplying = true;
        try
        {
            await CreatureCmd.Damage(
                choiceContext,
                base.Owner,
                base.Amount,
                ValueProp.Unpowered | ValueProp.Unblockable,
                null, null
            );
            await PowerCmd.Decrement(this);
        }
        finally
        {
            _isApplying = false;
        }
    }
    
    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != base.Owner || base.Amount <= 0)
            return Enumerable.Empty<HealthBarForecastSegment>();

        int count = (int)base.Amount;
        var segments = new List<HealthBarForecastSegment>();

        Color lightRed = new Color(1f, 0.6f, 0.6f);   // 浅红（左侧，对应短段）
        Color darkRed  = new Color(0.6f, 0.05f, 0.05f); // 深红（右侧，对应长段）

        for (int i = 0; i < count; i++)
        {
            float factor = (count == 1) ? 0f : (float)i / (count - 1);
            // 颜色从浅到深：i=0（最右边缘）→ 浅色，i=count-1（最左侧）→ 深色
            // 但由于 order 越大越远离边缘（越靠左），所以 order=i 时：
            // order 0（右边缘）为浅色，order 大（左侧）为深色 → 效果：左深右浅
            // 若要左浅右深，可改为 order = count-1 - i，并保持颜色 lerp 方向不变
            // 这里按你图示的“左浅右深”实现：order 从大到小，右侧浅
            int order = count - 1 - i; // 反转：i=0 -> order 大（左侧），颜色浅
            Color color = new Color(
                Mathf.Lerp(lightRed.R, darkRed.R, factor),
                Mathf.Lerp(lightRed.G, darkRed.G, factor),
                Mathf.Lerp(lightRed.B, darkRed.B, factor)
            );

            segments.Add(new HealthBarForecastSegment(
                1,                                          // 长度 1
                color,
                HealthBarForecastGrowthDirection.FromRight,
                order,                                      // 排列顺序
                null                                        // material
            ));
        }

        return segments;
    }
    //效果意外还行的渐变颜色条，备选
    
    /*
    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != base.Owner || base.Amount <= 0)
            return Enumerable.Empty<HealthBarForecastSegment>();

        var segments = new List<IEnumerable<HealthBarForecastSegment>>();

        Color lightRed = new Color(1f, 0.6f, 0.6f);   // 浅红（左侧长段）
        Color darkRed  = new Color(0.6f, 0.05f, 0.05f); // 深红（右侧短段）

        // i 从 Amount 递减到 1：i 大 = 长段 = 浅色，i 小 = 短段 = 深色
        for (int i = (int)base.Amount; i > 0; i--)
        {
            // i 越大颜色越浅，i 越小颜色越深
            float factor = (float)i / (float)base.Amount; // 1.0（浅）→ 接近0（深）
            Color segmentColor = new Color(
                Mathf.Lerp(darkRed.R, lightRed.R, factor),
                Mathf.Lerp(darkRed.G, lightRed.G, factor),
                Mathf.Lerp(darkRed.B, lightRed.B, factor)
            );

            segments.Add(HealthBarForecasts.Single(
                i,                                      // 长度递减：4,3,2,1
                segmentColor,
                HealthBarForecastGrowthDirection.FromRight
            ));
        }

        return segments.SelectMany(s => s);
    }
    */
    //我Chovy败给你了不要了
}