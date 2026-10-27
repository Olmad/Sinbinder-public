// Assets/Scripts/Core/FrameBudget.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sinbinder.Core
{
    /// <summary>
    /// Бережём машину игрока: потолок кадров и облегчённая графика.
    ///
    /// Автор, 2 октября: «Игра очень нагружает ПК, я давал игру на пробу,
    /// и я не единственная жертва». Причин нашлось две.
    ///
    /// <b>Потолка кадров не было.</b> Вертикальная синхронизация в обоих
    /// уровнях качества выключена, а <c>targetFrameRate</c> не ставил никто:
    /// игра рисовала столько кадров, сколько успевала, и держала видеокарту
    /// и процессор на пределе даже в тихом лагере. Теперь — шестьдесят,
    /// а когда окно не в фокусе — пятнадцать: свёрнутая игра не должна есть
    /// машину, пока игрок читает почту (игра идёт и в фоне — так настроено
    /// в проекте).
    ///
    /// <b>Кадр дорог для встроенной видеокарты.</b> Замер прогона
    /// (<c>[КАДР]</c>) на Intel UHD 630, лагерь у костра, кадр 1920×1080:
    /// 61 мс — шестнадцать кадров в секунду; без затенения углов (SSAO) —
    /// 40, без теней — 44. Облегчённая графика (<see cref="Preferences.LightGraphics"/>,
    /// строка «Графика» в паузе) снимает тени и затенение углов и рисует
    /// мир в 85% разрешения экрана (<see cref="LightScale"/>; интерфейс —
    /// в полном). По замеру «лучшее из трёх кругов» лагерь — 16 мс вместо 38:
    /// шестьдесят кадров на встроенной видеокарте; без доли разрешения
    /// было 20 — она упирается в заливку точек, а не в счёт. Взгляд (зерно,
    /// радугу краёв, свечение) не трогает: облегчённый он давал 0,6 мс —
    /// не цена за подпись стиля. Пока игрок не выбрал сам, выбор —
    /// по видеокарте: на встроенной облегчённая.
    ///
    /// <b>В редакторе</b> затенение углов и долю разрешения не трогаем:
    /// они живут в файлах настроек рендера, и тронутые в игре остались бы
    /// такими в проекте. Тени — свойство камеры сцены, их можно и там.
    ///
    /// <b>Прогон в пакетном режиме — без всего этого</b>: ему нужна скорость
    /// и полная картинка для снимков, а его замеры должны показывать работу,
    /// а не потолок.
    ///
    /// Ставит себя сам до первой сцены и живёт между сценами.
    /// </summary>
    public class FrameBudget : MonoBehaviour
    {
        /// <summary>Кадров в секунду в игре.</summary>
        public const int Target = 60;

        /// <summary>Кадров в секунду, пока окно не в фокусе.</summary>
        public const int Background = 15;

        /// <summary>Доля разрешения мира в облегчённой графике.</summary>
        public const float LightScale = 0.85f;

        private static FrameBudget _instance;

        /// <summary>Какая доля стояла до облегчённой; −1 — не трогали.</summary>
        private float _scaleWas = -1f;

        /// <summary>Камеры, которым мы выключили тени, — им и вернуть.</summary>
        private readonly List<UniversalAdditionalCameraData> _unshadowed = new();

        /// <summary>Выключенное затенение углов — вернуть при выходе и при «полной».</summary>
        private readonly List<ScriptableRendererFeature> _dimmed = new();

        private float _nextLook;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode || _instance != null) return;

            // Синхронизация выключена нарочно: с ней потолок — частота
            // монитора (на 144 Гц — 144 кадра), а targetFrameRate при ней
            // не действует. Шестьдесят — на любом мониторе.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Target;

            var go = new GameObject("Бережём машину");
            _instance = go.AddComponent<FrameBudget>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Применить выбор графики сейчас: зовёт строка «Графика» в паузе.</summary>
        public static void Apply()
        {
            if (_instance != null) _instance.Enforce();
        }

        void Start() => Enforce();

        void Update()
        {
            Watch();

            // Камеры приходят со сценой; проверять раз в секунду — хватит.
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 1f;
            Enforce();
        }

        // ---------------------------------------------------------- сторож кадра

        /// <summary>Окно, по которому судим, секунды.</summary>
        private const float Window = 3f;

        private float _windowFrom = -1f, _windowSum;
        private int _windowFrames, _slow, _fast;
        private bool _toldLighter;

        /// <summary>
        /// Сторож кадра (автор, 27.10: «система, если фиксирует недостаток, — переходит на
        /// облегчённый вариант»). Среднее за три секунды: два окна подряд медленнее цели
        /// на 15% — ступень облегчения вниз (<see cref="GraphicsOptions.AdaptStep"/>); пять
        /// окон подряд с запасом в 30% — обратно вверх. Вниз быстро, вверх медленно, чтобы
        /// картинка не мигала. Не судим: на паузе, без фокуса, первые секунды сцены (загрузка
        /// и выпечка навмеша — не мерило машины).
        /// </summary>
        private void Watch()
        {
            if (!GraphicsOptions.Adapt) return;

            bool paused = GamePauseController.Instance != null && GamePauseController.Instance.IsPaused;
            if (!_focused || paused || Time.timeSinceLevelLoad < 5f) { _windowFrom = -1f; return; }

            float now = Time.unscaledTime;
            if (_windowFrom < 0f) { _windowFrom = now; _windowSum = 0f; _windowFrames = 0; return; }

            _windowSum += Time.unscaledDeltaTime;
            _windowFrames++;
            if (now - _windowFrom < Window) return;

            float ms = _windowSum / Mathf.Max(1, _windowFrames) * 1000f;
            int target = GraphicsOptions.TargetFrames > 0 ? GraphicsOptions.TargetFrames : Target;
            float budget = 1000f / target;
            _windowFrom = now; _windowSum = 0f; _windowFrames = 0;

            if (ms > budget * 1.15f) { _slow++; _fast = 0; }
            else if (ms < budget * 0.7f) { _fast++; _slow = 0; }
            else { _slow = 0; _fast = 0; }

            if (_slow >= 2 && GraphicsOptions.AdaptStep < GraphicsOptions.AdaptMax)
            {
                GraphicsOptions.AdaptStep++;
                _slow = 0;
                Enforce();
                Debug.Log($"[ГРАФИКА] кадр {ms:F1} мс при цели {budget:F1} — облегчаю: ступень {GraphicsOptions.AdaptStep}.");
                if (!_toldLighter)
                {
                    _toldLighter = true;
                    Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(
                        Loc.T("Графика упрощена: машине тяжело. Отключить — «Графика» в паузе."));
                }
            }
            else if (_fast >= 5 && GraphicsOptions.AdaptStep > 0)
            {
                GraphicsOptions.AdaptStep--;
                _fast = 0;
                Enforce();
                Debug.Log($"[ГРАФИКА] кадр {ms:F1} мс — запас есть: ступень {GraphicsOptions.AdaptStep}.");
            }
        }

        void OnApplicationFocus(bool focused)
        {
            // В редакторе фокус уходит от окна игры к инспектору — игре
            // в нём незачем падать до пятнадцати кадров.
            if (Application.isEditor) return;
            _focused = focused;
            Frames();
        }

        private bool _focused = true;

        void OnApplicationQuit() => Wake();

        void OnDestroy() => Wake();

        /// <summary>
        /// Всё из <see cref="GraphicsOptions"/> (21 октября): тени, затенение
        /// углов, сглаживание, чёткость, зерно, потолок кадров. Раньше —
        /// одна «облегчённая» разом.
        /// </summary>
        private void Enforce()
        {
            Shadows(GraphicsOptions.ShadowsNow == GraphicsOptions.ShadowMode.Off);
            ShadowEdges();
            Occlusion(!GraphicsOptions.OcclusionNow);
            Detail();
            Smoothing();
            GraphicsOptions.ApplyGrain();
            Frames();
            MoonShadows();
        }

        /// <summary>
        /// Лунные тени по степени «Лунный свет»: ступени и дальность (URP — в файле
        /// настроек графики). Только в собранной игре: в редакторе правка ушла бы
        /// в сам файл проекта и в git. Лучи луны — <see cref="Gameplay.MoonShafts"/>.
        /// </summary>
        private static void MoonShadows()
        {
#if !UNITY_EDITOR
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return;
            var (cascades, distance) = GraphicsOptions.MoonNow switch
            {
                GraphicsOptions.MoonLevel.Low => (1, 28f),
                GraphicsOptions.MoonLevel.High => (4, 45f),
                _ => (2, 36f),
            };
            if (urp.shadowCascadeCount != cascades) urp.shadowCascadeCount = cascades;
            if (!Mathf.Approximately(urp.shadowDistance, distance)) urp.shadowDistance = distance;
#endif
        }

        private void Frames()
        {
            bool sync = GraphicsOptions.Frames == GraphicsOptions.FrameCap.Sync;
            QualitySettings.vSyncCount = sync ? 1 : 0;
            Application.targetFrameRate = _focused ? GraphicsOptions.TargetFrames : Background;
        }

        /// <summary>Мягкие или чёткие края теней — у каждого света с тенью.</summary>
        private static void ShadowEdges()
        {
            if (GraphicsOptions.ShadowsNow == GraphicsOptions.ShadowMode.Off) return;
            var want = GraphicsOptions.ShadowsNow == GraphicsOptions.ShadowMode.Hard
                ? LightShadows.Hard : LightShadows.Soft;

            // Точечные огни — всегда жёсткие: у них шесть граней карты теней, мягкие
            // края на каждой — замер 26.10 «Костёр» с жёсткими −2,5…−4 мс, а на глаз
            // у огня разницы почти нет. Мягкость — у луны.
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.shadows == LightShadows.None) continue;
                var edge = l.type == LightType.Point ? LightShadows.Hard : want;
                if (l.shadows != edge) l.shadows = edge;
            }
        }

        /// <summary>Сглаживание — свойство камеры, не файла настроек: можно и в редакторе.</summary>
        private static void Smoothing()
        {
            var mode = GraphicsOptions.Mode;
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null || data.renderType != CameraRenderType.Base) continue;
                if (data.antialiasing != mode) data.antialiasing = mode;
                if (mode == AntialiasingMode.SubpixelMorphologicalAntiAliasing)
                    data.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        /// <summary>
        /// Чёткость — доля разрешения мира и простое растяжение: мир рисуется
        /// мельче и растягивается в полный размер (STP Unity в сборке падал), на любой
        /// видеокарте. Файл настроек — только в собранной игре.
        /// </summary>
        private void Detail()
        {
            if (Application.isEditor) return;
            var pipe = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                       as UniversalRenderPipelineAsset;
            if (pipe == null) return;

            if (_scaleWas < 0f) { _scaleWas = pipe.renderScale; _filterWas = pipe.upscalingFilter; }

            float scale = GraphicsOptions.ScaleNow;
            if (!Mathf.Approximately(pipe.renderScale, scale)) pipe.renderScale = scale;
            // Пиксельная чёткость — растяжение без сглаживания, крупным чётким пикселем
            // (автор, 26.10: «пикселизация… запустить игру даже на картошке»).
            // Не STP: в собранной игре он падал (NullReferenceException в STP.Execute
            // каждый кадр — серый экран и зависание, автор 26.10 на «пиксельной»).
            // Простое растяжение — на любой машине.
            var filter = GraphicsOptions.Pixelated ? UpscalingFilterSelection.Point
                       : scale < 1f ? UpscalingFilterSelection.Linear : _filterWas;
            if (filter == UpscalingFilterSelection.STP) filter = UpscalingFilterSelection.Auto;
            if (pipe.upscalingFilter != filter) pipe.upscalingFilter = filter;
        }

        private UpscalingFilterSelection _filterWas = UpscalingFilterSelection.Auto;

        /// <summary>Тени — у каждой камеры; вернуть только тем, у кого сняли.</summary>
        private void Shadows(bool light)
        {
            _unshadowed.RemoveAll(d => d == null);

            if (!light)
            {
                foreach (var data in _unshadowed) data.renderShadows = true;
                _unshadowed.Clear();
                return;
            }

            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null || !data.renderShadows) continue;
                data.renderShadows = false;
                _unshadowed.Add(data);
            }
        }

        /// <summary>
        /// Затенение углов (SSAO) и доля разрешения — настройки рендера;
        /// только в собранной игре.
        /// </summary>
        private void Occlusion(bool light)
        {
            if (Application.isEditor) return;
            if (!light)
            {
                foreach (var f in _dimmed) if (f != null) f.SetActive(true);
                _dimmed.Clear();
                return;
            }

            if (_dimmed.Count > 0) return;

            // Класс затенения в URP закрыт — узнаём его по имени типа.
            foreach (var f in Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>())
            {
                if (!f.isActive || !f.GetType().Name.Contains("AmbientOcclusion")) continue;
                f.SetActive(false);
                _dimmed.Add(f);
            }
        }

        private void Wake()
        {
            foreach (var f in _dimmed) if (f != null) f.SetActive(true);
            _dimmed.Clear();

            if (_scaleWas < 0f) return;
            var pipe = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                       as UniversalRenderPipelineAsset;
            if (pipe != null) { pipe.renderScale = _scaleWas; pipe.upscalingFilter = _filterWas; }
            _scaleWas = -1f;
        }
    }
}
