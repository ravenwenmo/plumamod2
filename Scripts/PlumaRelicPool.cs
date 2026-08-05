using STS2RitsuLib.Scaffolding.Content;

namespace Pluma.Scripts;

public class PlumaRelicPool : TypeListRelicPoolModel
{
    // 描述中使用的能量图标。大小为24x24。
    public override string? TextEnergyIconPath => "res://pluma/images/energy_test.png";
    // tooltip和卡牌左上角的能量图标。大小为74x74。
    public override string? BigEnergyIconPath => "res://pluma/images/energy_test_big.png";

    public override string EnergyColorName => "pluma";
}