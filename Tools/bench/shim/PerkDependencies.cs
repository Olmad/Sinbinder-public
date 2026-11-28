// Only scene/audio dependencies of the production perk resolver are stubbed.
namespace Sinbinder.Core
{
    public static class LocationManager { public static bool HasTag(string tag) => false; }
}
namespace Sinbinder.Audio
{
    public class VoiceModifier { }
}
