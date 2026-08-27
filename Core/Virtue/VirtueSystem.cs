namespace Sinbinder.Core
{
    public class VirtueSystem
    {
        private SoulData _soul;

        public float Value => _soul.SinIntensity;
        public SinType Sin => _soul.Sin;
        public VirtueType Type => _soul.GetVirtueType();

        public VirtueSystem(SoulData soul)
        {
            _soul = soul;
        }

        public void Change(float amount)
        {
            _soul.ChangeIntensity(amount);
        }

        public string GetDescription()
        {
            return _soul.GetIntensityDescription();
        }
    }
}