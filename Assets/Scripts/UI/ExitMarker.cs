// Assets/Scripts/UI/ExitMarker.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Sinbinder.UI
{
    /// <summary>
    /// Где выход — словом на экране, пока уходить пора.
    ///
    /// Автор, 26 сентября: «Я не смог выйти из лагеря». Край открывался
    /// за внешним кольцом палаток, а сказано было только «нужно бежать» —
    /// куда, игрок угадывал сам: с высоты край прятался в тумане, от первого
    /// лица — за палатками. Метка стоит над воротами, а когда они вне кадра —
    /// у края экрана, со стрелкой в их сторону.
    ///
    /// Ставит её край, открываясь (<see cref="Gameplay.EscapeZone.Arm"/>);
    /// гаснет она сама, когда отряд ушёл или сцена сменилась. На паузе
    /// и в наезде её нет: там экран занят другим.
    /// </summary>
    public class ExitMarker : MonoBehaviour
    {
        private const string Word = "Выход";

        /// <summary>Отступ метки от края экрана, в точках.</summary>
        public const float Margin = 70f;

        /// <summary>Над землёй у ворот — чуть выше перекладины.</summary>
        private const float Above = 3.6f;

        private Gameplay.EscapeZone _zone;
        private Text _label;

        /// <summary>
        /// Метка на сцене одна, как и край. Уничтоженная сравнивается
        /// с null как пустая — Unity перегружает сравнение ровно для этого.
        /// </summary>
        private static ExitMarker _shown;

        /// <summary>Поставить метку края. Вторую не ставим.</summary>
        public static void Show(Gameplay.EscapeZone zone)
        {
            if (zone == null || _shown != null) return;

            var go = new GameObject("Метка выхода");
            SceneManager.MoveGameObjectToScene(go, zone.gameObject.scene);
            _shown = go.AddComponent<ExitMarker>();
            _shown._zone = zone;
        }

        void OnDestroy()
        {
            if (_shown == this) _shown = null;
        }

        /// <summary>
        /// Где рисовать метку и что на ней написать, если ворота легли
        /// в <paramref name="screen"/> (как отдаёт WorldToScreenPoint)
        /// на экране <paramref name="width"/>×<paramref name="height"/>.
        ///
        /// В кадре — над воротами, одним словом. Вне кадра — у края
        /// экрана по лучу из середины, со стрелкой туда, куда поворачивать.
        /// За спиной камеры луч отражается: иначе метка показывала бы
        /// ровно в противоположную сторону.
        /// </summary>
        public static Vector2 Place(Vector3 screen, float width, float height, out string text)
        {
            var centre = new Vector2(width * 0.5f, height * 0.5f);
            var at = new Vector2(screen.x, screen.y);
            bool behind = screen.z < 0f;

            bool inside = !behind
                       && at.x >= Margin && at.x <= width - Margin
                       && at.y >= Margin && at.y <= height - Margin;
            if (inside) { text = Word; return at; }

            var ray = at - centre;
            if (behind) ray = -ray;

            // Ровно по оси взгляда за спиной: луч нулевой, стороны нет.
            // Поворачивать всё равно надо — пусть будет вниз, «назад».
            if (ray.sqrMagnitude < 1f) ray = Vector2.down;

            float reachX = Mathf.Abs(ray.x) > 0.001f ? (centre.x - Margin) / Mathf.Abs(ray.x) : float.MaxValue;
            float reachY = Mathf.Abs(ray.y) > 0.001f ? (centre.y - Margin) / Mathf.Abs(ray.y) : float.MaxValue;
            var edge = centre + ray * Mathf.Min(reachX, reachY);

            if (Mathf.Abs(ray.x) >= Mathf.Abs(ray.y))
                text = ray.x > 0f ? Word + " →" : "← " + Word;
            else
                text = ray.y > 0f ? "↑ " + Word : "↓ " + Word;

            return edge;
        }

        void Start() => Build();

        void LateUpdate()
        {
            if (_zone == null || _zone.Departing) { Destroy(gameObject); return; }
            if (_label == null) return;

            var cam = Camera.main;
            var pause = Core.GamePauseController.Instance;
            var frame = Dialogue.DialogueCameraController.Instance;

            bool hidden = cam == null || !_zone.Open
                       || (pause != null && pause.IsPaused)
                       || (frame != null && frame.InDialogue);

            _label.enabled = !hidden;
            if (hidden) return;

            var screen = cam.WorldToScreenPoint(_zone.transform.position + Vector3.up * Above);
            var at = Place(screen, Screen.width, Screen.height, out string text);

            _label.text = text;
            _label.rectTransform.position = new Vector3(at.x, at.y, 0f);
        }

        private void Build()
        {
            var canvasGo = new GameObject("Холст метки выхода", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19;

            var any = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            var font = any != null && any.font != null
                ? any.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Выход", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);

            _label = go.AddComponent<Text>();
            _label.font = font;
            _label.fontSize = 28;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;

            // Холодный, как свет ворот: метка и место говорят одно.
            _label.color = new Color(0.72f, 0.82f, 0.98f);
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            _label.rectTransform.sizeDelta = new Vector2(260f, 40f);
            _label.enabled = false;
        }
    }
}
