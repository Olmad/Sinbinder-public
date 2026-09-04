// Assets/Scripts/Audio/VoiceModifier.cs
namespace Sinbinder.Audio
{
    /// <summary>
    /// Как перк искажает голос воина в VoiceGenerator.
    /// Множители равны 1 — «ничего не менять».
    /// </summary>
    [System.Serializable]
    public class VoiceModifier
    {
        public bool OverrideVoiceType;
        public VoiceGenerator.VoiceType VoiceType = VoiceGenerator.VoiceType.Square;
        public float PitchMultiplier = 1f;
        public float DurationMultiplier = 1f;
    }
}
