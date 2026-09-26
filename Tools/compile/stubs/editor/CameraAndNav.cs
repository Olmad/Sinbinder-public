// Заглушки: данные камеры URP и сбор объектов навигации.
namespace UnityEngine.Rendering.Universal
{
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing;
        public AntialiasingMode antialiasing;
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null;
    }
}
