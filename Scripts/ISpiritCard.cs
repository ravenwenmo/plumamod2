namespace Pluma.Scripts
{
    public interface ISpiritCard
    {
        SpiritMode CurrentMode { get; }
    }

    public enum SpiritMode
    {
        Self,
        Enemy,
        Ally
    }
}