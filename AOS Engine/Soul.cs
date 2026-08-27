// Assets/_Project/Scripts/AOS Engine/Soul.cs
namespace Sinbinder.AOS
{
    /// <summary>
    /// Временный адаптер для передачи данных о личности в модули AOS.
    /// В будущем будет заменён на прямую работу с SoulData.
    /// </summary>
    public class Soul
    {
        public string Name;
        public float SinIntensity; // -100..+100
        public MoralityType Morality;
        public float Loyalty; // 0..100
    }

    /// <summary>
    /// Временный enum. В будущем будет заменён на MoralType из Core.
    /// </summary>
    public enum MoralityType
    {
        Vicious,
        Neutral,
        Pious
    }
}