// Заглушка Volume из Core RP и эффектов URP: только то, что зовут сборщики.
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public class VolumeParameter<T>
    {
        public bool overrideState;
        public T value;
    }

    public class FloatParameter : VolumeParameter<float> { }
    public class ClampedFloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : VolumeParameter<float> { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class Vector4Parameter : VolumeParameter<Vector4> { }

    public class VolumeComponent : ScriptableObject { }

    public class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public bool Has<T>() where T : VolumeComponent => false;
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal;
        public float priority;
        public float weight;
        public VolumeProfile sharedProfile;
        public VolumeProfile profile;
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum FilmGrainLookup { Thin1, Thin2, Medium1, Medium2, Medium3, Medium4, Medium5, Medium6, Large01, Large02, Custom }
    public enum TonemappingMode { None, Neutral, ACES }

    public class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup> { }
    public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }

    public class ColorAdjustments : VolumeComponent
    { public ClampedFloatParameter saturation, contrast, hueShift; public FloatParameter postExposure; }
    public class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; }
    public class Vignette : VolumeComponent { public ClampedFloatParameter intensity, smoothness; public ColorParameter color; }
    public class SplitToning : VolumeComponent
    { public ColorParameter shadows, highlights; public ClampedFloatParameter balance; }
    public class LiftGammaGain : VolumeComponent { public Vector4Parameter lift, gamma, gain; }
    public class FilmGrain : VolumeComponent
    { public FilmGrainLookupParameter type; public ClampedFloatParameter intensity, response; }
    public class Bloom : VolumeComponent
    { public MinFloatParameter threshold, intensity; public ClampedFloatParameter scatter; }
    public class ChromaticAberration : VolumeComponent { public ClampedFloatParameter intensity; }
    public class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
}
