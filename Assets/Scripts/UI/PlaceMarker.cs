// Assets/Scripts/UI/PlaceMarker.cs
// Перевод: текст через Loc (слово метки переводит тот, кто её ставит)
using System;
using UnityEngine;
using UnityEngine.UI;

using Sinbinder.Gameplay;
namespace Sinbinder.UI
{
    /// <summary>
    /// Куда идти — словом на экране, как у выхода из лагеря (<see cref="ExitMarker"/>).
    ///
    /// Автор, 06.11: «Нужно уточнение подсказок — если есть конкретное место или
    /// примерное место — показывать стрелкой как выход из лагеря». Задача говорит
    /// «откройте сундук Марги», а где сундук, игрок искал сам. Теперь над местом —
    /// его имя, а когда оно вне кадра — у края экрана, со стрелкой в его сторону.
    /// Тот же расчёт, что у выхода (<see cref="ExitMarker.Place(Vector3,float,float,string,out string)"/>).
    ///
    /// Метку держит тот, кому есть что показать: задачи лагеря (<see cref="TaskList"/>)
    /// и подсказка о жатве (<see cref="HarvestHintUI"/>). У каждого своя. Пришёл
    /// на место — метка гаснет: дальше говорит само место. На паузе, в наезде
    /// и в окнах её нет: экран занят другим.
    /// </summary>
    public class PlaceMarker : MonoBehaviour
    {
        private Func<Vector3?> _where;
        private string _word;
        private float _near;
        private Text _label;

        /// <summary>Хозяин прячет метку на время (задачи молчат, пока говорит сцена).</summary>
        public bool Hidden { get; set; }

        /// <summary>Что метка показывает сейчас — для прогона; пусто, если не видна.</summary>
        public string Shown => _label != null && _label.enabled ? _label.text : "";

        /// <summary>Слово, на которое метка нацелена (видна она или нет), — для прогона.</summary>
        public string Word => _where != null ? _word : "";

        /// <summary>Метка на хозяине: своим цветом, пока ни на что не нацелена.</summary>
        public static PlaceMarker On(GameObject owner, Color colour)
        {
            var go = new GameObject("Метка места");
            go.transform.SetParent(owner.transform, false);
            var marker = go.AddComponent<PlaceMarker>();
            marker.Build(colour);
            return marker;
        }

        /// <summary>
        /// Нацелить: <paramref name="word"/> — переведённое имя места; <paramref name="where"/>
        /// зовётся каждый кадр и отдаёт точку над местом (null — места сейчас нет);
        /// ближе <paramref name="near"/> метров к нему — метка гаснет.
        /// </summary>
        public void Aim(string word, Func<Vector3?> where, float near)
        {
            if (_word != word || _where == null)
                Debug.Log($"[МЕТКА] {word}");
            _word = word;
            _where = where;
            _near = near;
        }

        /// <summary>Снять: показывать больше нечего.</summary>
        public void Clear()
        {
            _where = null;
            if (_label != null) _label.enabled = false;
        }

        void LateUpdate()
        {
            if (_label == null) return;

            var cam = Camera.main;
            var pause = Core.GamePauseController.Instance;
            var frame = Dialogue.DialogueCameraController.Instance;
            Vector3? at = null;
            bool hidden = Hidden || _where == null || cam == null
                       || (pause != null && pause.IsPaused)
                       || (frame != null && frame.InDialogue)
                       || Modal.Any || CrystalBall.Gazing;
            if (!hidden)
            {
                try { at = _where(); }
                catch (Exception e) { Debug.LogException(e); _where = null; }
                hidden = !at.HasValue
                      || (SinbinderPlayer.Exists && CampFocus.GroundDistance(SinbinderPlayer.Where, at.Value) < _near);
            }

            _label.enabled = !hidden;
            if (hidden) return;

            var screen = cam.WorldToScreenPoint(at.Value);
            var spot = ExitMarker.Place(screen, Screen.width, Screen.height, _word, out string text);
            _label.text = text;
            ExitMarker.Pin(_label.rectTransform, spot);
        }

        private void Build(Color colour)
        {
            var canvasGo = new GameObject("Холст метки места", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;   // под меткой выхода: уходить важнее

            var go = new GameObject("Место", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);

            _label = go.AddComponent<Text>();
            _label.font = UiStyle.Body;
            _label.fontSize = 26;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            _label.color = colour;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);

            _label.rectTransform.sizeDelta = new Vector2(320f, 40f);
            _label.enabled = false;
        }
    }
}
