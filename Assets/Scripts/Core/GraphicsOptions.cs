// Assets/Scripts/Core/GraphicsOptions.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sinbinder.Core
{
    /// <summary>
    /// Настройки картинки по отдельности. Автор, 21 октября: «сделай нам
    /// настройки, которые нам реально помогут, и не забудь сделать
    /// возможность переключать, выключать». До этого была одна строка
    /// «Графика: полная / облегчённая» (<see cref="FrameBudget"/>).
    ///
    /// Только то, что видно или ощутимо: тени, затенение углов, сглаживание,
    /// чёткость (доля разрешения мира с масштабатором STP — свой у Unity,
    /// работает на любой видеокарте, в отличие от DLSS), зерно и виньетка,
    /// потолок кадров. Наборы «полная» и «облегчённая» ставят всё разом;
    /// тронул пункт сам — набор становится «своя».
    ///
    /// Цифр игрок не видит — только слова. Помнится в <c>PlayerPrefs</c>.
    /// </summary>
    public static class GraphicsOptions
    {
        public enum ShadowMode { Off, Soft, Hard }
        public enum Smoothing { Off, Fast, Sharp, Temporal }
        public enum Sharpness { Full, High, Medium }
        public enum FrameCap { Thirty, Sixty, None, Sync }

        public static ShadowMode Shadows { get; private set; } = ShadowMode.Soft;
        public static bool Occlusion { get; private set; } = true;
        public static Smoothing Antialias { get; private set; } = Smoothing.Fast;
        public static Sharpness Detail { get; private set; } = Sharpness.Full;
        public static bool Grain { get; private set; } = true;
        public static FrameCap Frames { get; private set; } = FrameCap.Sixty;

        private const string Key = "sinbinder.gfx.";

        /// <summary>Облегчённая ли вся картинка — как прежний набор.</summary>
        public static bool IsLight => Shadows == ShadowMode.Off && !Occlusion && Detail != Sharpness.Full;

        private static bool IsFull => Shadows == ShadowMode.Soft && Occlusion && Detail == Sharpness.Full
                                   && Antialias == Smoothing.Fast && Grain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            // Первый запуск: набор по видеокарте, как раньше (Preferences.Integrated).
            // Порядок двух BeforeSceneLoad не задан — прежний выбор читаем сами.
            if (!PlayerPrefs.HasKey(Key + "shadows"))
            {
                bool light = PlayerPrefs.HasKey("sinbinder.graphics.light")
                    ? PlayerPrefs.GetInt("sinbinder.graphics.light", 0) != 0
                    : Preferences.Integrated();
                if (light) Light(); else Full();
                return;
            }

            Shadows = (ShadowMode)PlayerPrefs.GetInt(Key + "shadows", (int)ShadowMode.Soft);
            Occlusion = PlayerPrefs.GetInt(Key + "ao", 1) != 0;
            Antialias = (Smoothing)PlayerPrefs.GetInt(Key + "aa", (int)Smoothing.Fast);
            Detail = (Sharpness)PlayerPrefs.GetInt(Key + "scale", (int)Sharpness.Full);
            Grain = PlayerPrefs.GetInt(Key + "grain", 1) != 0;
            Frames = (FrameCap)PlayerPrefs.GetInt(Key + "fps", (int)FrameCap.Sixty);
        }

        // ---------------------------------------------------------- наборы

        public static string PresetName()
            => IsFull ? Loc.T("полная") : IsLight ? Loc.T("облегчённая") : Loc.T("своя");

        /// <summary>Полная → облегчённая → полная. «Своя» уходит в полную.</summary>
        public static void CyclePreset()
        {
            if (IsFull) Light(); else Full();
            Save();
        }

        private static void Full()
        {
            Shadows = ShadowMode.Soft; Occlusion = true; Antialias = Smoothing.Fast;
            Detail = Sharpness.Full; Grain = true;
        }

        /// <summary>Замер 2 октября на Intel UHD 630: лагерь 16 мс вместо 38 (FrameBudget).</summary>
        private static void Light()
        {
            Shadows = ShadowMode.Off; Occlusion = false; Antialias = Smoothing.Fast;
            Detail = Sharpness.High; Grain = true;
        }

        // ---------------------------------------------------------- пункты

        public static string ShadowsName() => Shadows switch
        {
            ShadowMode.Off => Loc.T("выключены"),
            ShadowMode.Hard => Loc.T("чёткие"),
            _ => Loc.T("мягкие"),
        };

        public static void CycleShadows() { Shadows = (ShadowMode)(((int)Shadows + 1) % 3); Save(); }

        public static string OcclusionName() => Occlusion ? Loc.T("включено") : Loc.T("выключено");

        public static void ToggleOcclusion() { Occlusion = !Occlusion; Save(); }

        public static string AntialiasName() => Antialias switch
        {
            Smoothing.Off => Loc.T("выключено"),
            Smoothing.Sharp => Loc.T("чёткое (SMAA)"),
            Smoothing.Temporal => Loc.T("временное (TAA) — мягче, без лесенки"),
            _ => Loc.T("быстрое (FXAA)"),
        };

        public static void CycleAntialias() { Antialias = (Smoothing)(((int)Antialias + 1) % 4); Save(); }

        public static string DetailName() => Detail switch
        {
            Sharpness.High => Loc.T("высокая — мир чуть мягче, кадр легче"),
            Sharpness.Medium => Loc.T("средняя — для слабой видеокарты"),
            _ => Loc.T("полная"),
        };

        public static void CycleDetail() { Detail = (Sharpness)(((int)Detail + 1) % 3); Save(); }

        public static string GrainName() => Grain ? Loc.T("включены") : Loc.T("выключены");

        public static void ToggleGrain() { Grain = !Grain; Save(); }

        public static string FramesName() => Frames switch
        {
            FrameCap.Thirty => Loc.T("тридцать в секунду — тише и холоднее"),
            FrameCap.None => Loc.T("без потолка"),
            FrameCap.Sync => Loc.T("по частоте монитора"),
            _ => Loc.T("шестьдесят в секунду"),
        };

        public static void CycleFrames() { Frames = (FrameCap)(((int)Frames + 1) % 4); Save(); }

        private static void Save()
        {
            PlayerPrefs.SetInt(Key + "shadows", (int)Shadows);
            PlayerPrefs.SetInt(Key + "ao", Occlusion ? 1 : 0);
            PlayerPrefs.SetInt(Key + "aa", (int)Antialias);
            PlayerPrefs.SetInt(Key + "scale", (int)Detail);
            PlayerPrefs.SetInt(Key + "grain", Grain ? 1 : 0);
            PlayerPrefs.SetInt(Key + "fps", (int)Frames);
            PlayerPrefs.Save();
            FrameBudget.Apply();
        }

        // ---------------------------------------------------------- применить

        /// <summary>Доля разрешения мира по чёткости.</summary>
        public static float Scale => Detail switch
        {
            Sharpness.High => 0.85f,
            Sharpness.Medium => 0.7f,
            _ => 1f,
        };

        public static AntialiasingMode Mode => Antialias switch
        {
            Smoothing.Off => AntialiasingMode.None,
            Smoothing.Sharp => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            Smoothing.Temporal => AntialiasingMode.TemporalAntiAliasing,
            _ => AntialiasingMode.FastApproximateAntialiasing,
        };

        /// <summary>Потолок кадров в фокусе; −1 — без потолка.</summary>
        public static int TargetFrames => Frames switch
        {
            FrameCap.Thirty => 30,
            FrameCap.None => -1,
            FrameCap.Sync => -1,
            _ => FrameBudget.Target,
        };

        /// <summary>
        /// Зерно, виньетка и радуга краёв — в профиле «Взгляд» и поверх
        /// интерфейса (<see cref="UI.ScreenGrain"/>). Профиль трогаем копией
        /// у объема (<c>Volume.profile</c>), не сам файл.
        /// </summary>
        public static void ApplyGrain()
        {
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v == null || v.sharedProfile == null) continue;
                var p = v.profile;
                if (p.TryGet<FilmGrain>(out var g)) g.active = Grain;
                if (p.TryGet<Vignette>(out var vg)) vg.active = Grain;
                if (p.TryGet<ChromaticAberration>(out var ca)) ca.active = Grain;
            }

            var overlay = Object.FindFirstObjectByType<UI.ScreenGrain>(FindObjectsInactive.Include);
            if (overlay != null && overlay.gameObject.activeSelf != Grain) overlay.gameObject.SetActive(Grain);
        }
    }
}
