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
    /// 40, без теней — 44. Облегчённая графика снимает и то и другое
    /// (<see cref="Preferences.LightGraphics"/>, строка «Графика» в паузе).
    /// Пока игрок не выбрал сам, выбор — по видеокарте: на встроенной
    /// облегчённая.
    ///
    /// <b>В редакторе</b> затенение углов не трогаем: признак живёт в файле
    /// настроек рендера, и выключенный в игре остался бы выключенным
    /// в проекте. Тени — свойство камеры сцены, их можно и там.
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

        private static FrameBudget _instance;

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
            // Камеры приходят со сценой; проверять раз в секунду — хватит.
            if (Time.unscaledTime < _nextLook) return;
            _nextLook = Time.unscaledTime + 1f;
            Enforce();
        }

        void OnApplicationFocus(bool focused)
        {
            // В редакторе фокус уходит от окна игры к инспектору — игре
            // в нём незачем падать до пятнадцати кадров.
            if (Application.isEditor) return;
            Application.targetFrameRate = focused ? Target : Background;
        }

        void OnApplicationQuit() => Wake();

        void OnDestroy() => Wake();

        private void Enforce()
        {
            bool light = Preferences.LightGraphics;
            Shadows(light);
            Occlusion(light);
        }

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

        /// <summary>Затенение углов (SSAO) — признак рендера; только в собранной игре.</summary>
        private void Occlusion(bool light)
        {
            if (Application.isEditor) return;
            if (!light) { Wake(); return; }
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
        }
    }
}
