// Assets/Scripts/UI/ScreenGrain.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;

namespace Sinbinder.UI
{
    /// <summary>
    /// Зерно и виньетка поверх интерфейса (docs/42-INTERFACE.md §1, п. 8;
    /// 41-SHOWCASE п. 2).
    ///
    /// Профиль «Взгляд» (<c>EffectsBuilder.Look</c>) кладёт зерно и виньетку
    /// на кадр, но холсты Screen Space – Overlay постобработку не получают:
    /// полоса, журнал и подсказки лежали поверх зернистой ночи чистыми,
    /// как наклейка. Здесь — тот же приём для интерфейса: тонкий слой поверх
    /// всего, мягко темнее по углам и с едва заметным зерном.
    ///
    /// Щелчков не ловит. Зерно — по хешу номера кадра, а не по жребию:
    /// «ничего случайного» относится к решениям, но и здесь одинаковый
    /// кадр — одинаковое зерно.
    ///
    /// Ставит себя сам и живёт между сценами, как панель приказов.
    /// </summary>
    public class ScreenGrain : MonoBehaviour
    {
        private static ScreenGrain _instance;

        /// <summary>
        /// Сила затемнения углов. Мягко: виньетка «Взгляда» уже темнит мир
        /// под интерфейсом, и эта ложится поверх неё; у полосы по краям — текст.
        /// </summary>
        private const float Vignette = 0.25f;

        /// <summary>Сила зерна — наибольшая прозрачность крапинки. Его не видят — его чувствуют.</summary>
        private const float Grain = 0.12f;

        private const int NoiseSize = 128;
        private const float GrainFps = 24f;

        private RawImage _grain;
        private float _nextShift;
        private int _shift;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Зерно и виньетка");
            _instance = go.AddComponent<ScreenGrain>();
            DontDestroyOnLoad(go);
        }

        void Start()
        {
            var canvasGo = new GameObject("Холст зерна", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;   // над интерфейсом, под модальными окнами не важно — щелчков не ловит

            var vignette = Layer(canvasGo.transform, "Виньетка");
            vignette.texture = VignetteTexture();

            _grain = Layer(canvasGo.transform, "Зерно");
            _grain.texture = NoiseTexture();
        }

        void Update()
        {
            if (_grain == null || Time.unscaledTime < _nextShift) return;
            _nextShift = Time.unscaledTime + 1f / GrainFps;

            // Зерно живое: каждый шаг — новый сдвиг узора, по хешу шага.
            _shift++;
            uint h = Hash((uint)_shift);
            float w = Screen.width / (float)NoiseSize, hgt = Screen.height / (float)NoiseSize;
            _grain.uvRect = new Rect((h & 0xFFFF) / 65535f, (h >> 16) / 65535f, w, hgt);
        }

        private static RawImage Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var image = go.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Чёрный, прозрачный в середине и плотнее к углам.</summary>
        private static Texture2D VignetteTexture()
        {
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;          // 0 в центре, 1 в углу
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r)) * Vignette;
                px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>
        /// Зерно только темнящее: чёрные крапинки разной прозрачности.
        ///
        /// Первый заход был серым шумом «светлее и темнее поровну» — и в кадре
        /// прогона 1 октября весь экран встал снегом. Проект линейный: серый
        /// на пять процентов поверх почти чёрной ночи после гаммы осветляет
        /// тёмное в разы. Темнящее зерно в темноте почти не видно, а на светлых
        /// буквах и панелях даёт фактуру — то, ради чего оно и нужно.
        /// </summary>
        private static Texture2D NoiseTexture()
        {
            var t = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };
            var px = new Color32[NoiseSize * NoiseSize];
            for (int i = 0; i < px.Length; i++)
            {
                float v = (Hash((uint)i * 2654435761u) & 0xFF) / 255f;
                px[i] = new Color32(0, 0, 0, (byte)(v * Grain * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Хеш без состояния: одинаковый вход — одинаковый выход.</summary>
        private static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d;
            x ^= x >> 15; x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }
    }
}
