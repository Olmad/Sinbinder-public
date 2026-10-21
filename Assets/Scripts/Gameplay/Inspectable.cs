// Assets/Scripts/Gameplay/Inspectable.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Вещь, которую рассматривают: подошёл — «F — …», нажал — мир стоит,
    /// камера наезжает на неё, на нижней полосе по букве печатаются строки
    /// (<see cref="UI.Letterbox"/>), будто вещь рассказывает сама.
    ///
    /// Заведено 21 октября под две просьбы автора: дерево с воткнутым
    /// топором и вырезанным на коре дневником; пешка в палатке, которая
    /// «рассказывает историю» — не подбирается, а говорит.
    ///
    /// F один на всех: его ведёт <see cref="UI.GearPanel"/> — сперва воин,
    /// потом сундук, потом ближайшая из этих вещей (<see cref="Nearest"/>).
    /// Строки хранятся по-русски и переводятся при показе (Loc.T).
    /// </summary>
    public class Inspectable : MonoBehaviour
    {
        [SerializeField] private string _prompt = Loc.N("F — осмотреть");
        [SerializeField] private string _speaker = "";
        [SerializeField] private string[] _lines = new string[0];

        [Tooltip("Высота, на которую смотрит камера, м.")]
        [SerializeField] private float _focus = 1.3f;

        [Tooltip("С какого расстояния смотрит камера, м.")]
        [SerializeField] private float _distance = 2.2f;

        [Tooltip("С какого расстояния по земле можно рассмотреть, м.")]
        [SerializeField] private float _reach = 2.6f;

        [Tooltip("Где у вещи перёд, в её осях: туда смотрит то, что рассматривают.")]
        [SerializeField] private Vector3 _front = Vector3.forward;

        [Tooltip("Имя текстов в InspectTexts. Задано — строки берутся оттуда при запуске.")]
        [SerializeField] private string _texts = "";

        /// <summary>Тексты по имени — из InspectTexts (правятся без пересборки сцен).</summary>
        public void UseTexts(string key) => _texts = key;

        [Tooltip("Говорит голосом Смерти: тишина вокруг, огни меркнут, печать медленнее.")]
        [SerializeField] private bool _death;

        /// <summary>Голос Смерти — для пешки (Audio.DeathVoice).</summary>
        public void UseDeath() => _death = true;

        void Awake()
        {
            if (string.IsNullOrEmpty(_texts)) return;
            var t = InspectTexts.For(_texts);
            if (t == null) { Debug.LogWarning($"[ОСМОТР] {name}: текстов «{_texts}» нет в InspectTexts"); return; }
            (_prompt, _speaker, _lines) = t.Value;
        }

        private static readonly List<Inspectable> Live = new();

        /// <summary>Идёт ли рассказ какой-нибудь вещи. Спрашивает прогон.</summary>
        public static bool Telling { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Live.Clear(); Telling = false; }

        /// <summary>Задать из сборщика сцены. Строки — русские (Loc.N), перевод при показе.</summary>
        public void Configure(string prompt, string speaker, string[] lines,
                              float focus = 1.3f, float distance = 2.2f, float reach = 2.6f,
                              Vector3? front = null)
        {
            _front = front ?? Vector3.forward;
            _prompt = prompt;
            _speaker = speaker;
            _lines = lines ?? new string[0];
            _focus = focus;
            _distance = distance;
            _reach = reach;
        }

        public string Prompt => Loc.T(_prompt);

        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() => Live.Remove(this);

        /// <summary>Насколько вбок от взгляда ещё «смотрит на вещь», градусов.</summary>
        private const float LookCone = 22f;

        /// <summary>
        /// Какую вещь Греховод сейчас рассматривал бы. Автор, 21 октября:
        /// «игрок взаимодействует с объектом, на который смотрит, или который
        /// просто рядом?» От первого лица — на которую смотрит (в пределах
        /// руки и в конусе взгляда), как с воинами; сверху взгляда нет —
        /// ближайшая. Нет — null.
        /// </summary>
        public static Inspectable Nearest()
        {
            if (!SinbinderPlayer.Exists || Telling) return null;

            var cam = Camera.main;
            var view = cam != null ? cam.GetComponent<RTS_Camera>() : null;
            bool looking = view != null && view.FirstPersonNow;

            Inspectable best = null;
            float bestScore = float.MaxValue;
            foreach (var i in Live)
            {
                if (i == null || i._lines.Length == 0) continue;
                float d = CampFocus.GroundDistance(SinbinderPlayer.Where, i.transform.position);
                if (d > i._reach) continue;

                float score = d;
                if (looking)
                {
                    var to = i.transform.position + Vector3.up * i._focus - cam.transform.position;
                    float angle = Vector3.Angle(cam.transform.forward, to);
                    if (angle > LookCone) continue;
                    score = angle;
                }

                if (score < bestScore) { best = i; bestScore = score; }
            }
            return best;
        }

        public void Inspect()
        {
            if (Telling || _lines.Length == 0) return;
            StartCoroutine(Tell());
        }

        private IEnumerator Tell()
        {
            var camera = Dialogue.DialogueCameraController.Instance;
            var pause = GamePauseController.Instance;
            if (camera == null) yield break;

            Telling = true;
            int mine = -1;
            if (pause != null) { pause.Pause(); mine = pause.Stamp; }
            camera.SaveCameraPosition();

            // На перёд вещи (+Z — там знаки), под 35° сбоку и чуть сверху:
            // из-за спины Греховода он сам вставал в пол-кадра (прогон 21.10).
            var look = transform.position + Vector3.up * _focus;
            var front = transform.TransformDirection(_front);
            front.y = 0f;
            if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
            var dir = Quaternion.Euler(0f, 35f, 0f) * front.normalized;
            var eye = look + dir * _distance + Vector3.up * 0.6f;

            // Греховод подходит как раз с той стороны, куда смотрит кадр, и
            // вставал в пол-экрана (кадры 21.10) — на время рассказа его не видно.
            var hidden = new List<Renderer>();
            if (SinbinderPlayer.Instance != null)
                foreach (var r in SinbinderPlayer.Instance.GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }

            Debug.Log($"[ОСМОТР] {name}: наезд, строк {_lines.Length}");
            yield return camera.FocusOnPoint(look, eye);

            var box = UI.Letterbox.Instance;
            float pace = box != null ? box.LettersPerSecond : 40f;
            if (_death)
            {
                Audio.DeathVoice.Begin(transform.position);
                if (box != null) box.LettersPerSecond = Audio.DeathVoice.Letters;
                yield return new WaitForSecondsRealtime(1.2f);   // тишина опускается
            }

            string who = string.IsNullOrEmpty(_speaker) ? null : Loc.T(_speaker);
            foreach (var raw in _lines)
            {
                string line = Loc.T(raw);
                box = UI.Letterbox.Instance;
                box?.Say(who, line);
                float hold = Herald.Hold(line) + 0.6f;
                if (_death) hold = Mathf.Max(hold, Audio.DeathVoice.Say(line) + 1.6f);
                yield return new WaitForSecondsRealtime(hold);
            }

            if (_death)
            {
                Audio.DeathVoice.End();
                if (UI.Letterbox.Instance != null) UI.Letterbox.Instance.LettersPerSecond = pace;
            }

            camera.StopSway();
            yield return camera.RestoreCamera();
            UI.Letterbox.Instance?.Hide();
            foreach (var r in hidden) if (r != null) r.enabled = true;

            if (pause != null && pause.IsPaused && pause.Stamp == mine) pause.Resume();
            Telling = false;
        }
    }
}
