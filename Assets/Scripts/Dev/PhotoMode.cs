// Assets/Scripts/Dev/PhotoMode.cs
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Фоторежим для трейлера (автор, 29.10: «надо сделать несколько камер с фоторежимом, чтобы отснять трейлер»).
    /// Есть и в сборке: это не рычаг игры, а камера. CaptureMode (F10–F12) остаётся для редактора.
    ///
    /// * <b>F8</b> — вкл/выкл: интерфейс прячется, камера свободная, игра не слышит мышь и клавиши.
    /// * <b>WASD</b> — лететь, <b>Q/E</b> — ниже/выше, <b>Shift</b> — быстрее, <b>Ctrl</b> — медленнее; правая
    ///   кнопка мыши зажата — смотреть; колесо — угол обзора.
    /// * <b>T</b> — время: обычное → медленно → стоп → обычное.
    /// * <b>Ctrl+1…9</b> — запомнить камеру; <b>1…9</b> — плавно перейти к ней. Камеры помнятся между запусками.
    /// * <b>Пробел</b> — проезд по запомненным камерам по порядку, плавной кривой; <b>[ ]</b> — медленнее/быстрее.
    /// * <b>R</b> — камера привязана к ворону (держит то положение, в каком привязали: встань сбоку — снимет сбоку);
    ///   <b>G</b> — к тому, что под мышью; ещё раз — отвязать.
    /// * <b>P</b> — снимок в двойном разрешении в папку Screenshots рядом с игрой; <b>H</b> — подсказка.
    /// </summary>
    public class PhotoMode : MonoBehaviour
    {
        public static bool Active { get; private set; }

        private Camera _cam;
        private Vector3 _wasPos;
        private Quaternion _wasRot;
        private float _wasFov;
        private readonly List<Behaviour> _paused = new();
        private bool _cleanBefore;
        private float _yaw, _pitch;
        private int _timeStep;
        private float _helpUntil;

        private Vector3 _blendFromPos, _blendToPos;
        private Quaternion _blendFromRot, _blendToRot;
        private float _blendFromFov, _blendToFov, _blendT = -1f;

        private bool _playing;
        private float _playT;
        private float _segment = 4f;
        private List<int> _route;

        private const string Key = "Sinbinder.Photo.";   // ключ PlayerPrefs

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<PhotoMode>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("Фоторежим");
            go.AddComponent<PhotoMode>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) { if (Active) Off(); else On(); }
            if (!Active) return;
            if (_cam == null) { Off(); return; }

            if (Input.GetKeyDown(KeyCode.H)) _helpUntil = _helpUntil > Time.unscaledTime ? 0f : Time.unscaledTime + 30f;
            if (Input.GetKeyDown(KeyCode.T)) Step();
            if (Input.GetKeyDown(KeyCode.P)) Shot();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) _segment = Mathf.Min(20f, _segment * 1.25f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) _segment = Mathf.Max(0.8f, _segment / 1.25f);
            if (Input.GetKeyDown(KeyCode.Space)) { if (_playing) _playing = false; else Play(); }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            for (int i = 1; i <= 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + i)) continue;
                if (ctrl) Save(i); else BlendTo(i);
            }

            if (Input.GetKeyDown(KeyCode.R)) { if (_follow != null) Unfollow(); else FollowRaven(); }
            if (Input.GetKeyDown(KeyCode.L)) { var bird = Sinbinder.Gameplay.RavenFlight.LaunchAny(); _note = bird != null ? Loc.T("Ворон поднят") : Loc.T("Ворон уже в полёте или его нет"); _helpUntil = Time.unscaledTime + 1.5f; }
            if (Input.GetKeyDown(KeyCode.G)) { if (_follow != null) Unfollow(); else FollowUnderMouse(); }

            if (_playing) { Playing(); return; }
            if (_blendT >= 0f) { Blending(); return; }
            if (_follow != null) { Following(); return; }
            Fly(ctrl);
        }

        // ---------------------------------------------------------- вкл / выкл

        private void On()
        {
            _cam = Camera.main;
            if (_cam == null) return;
            Active = true;
            _wasPos = _cam.transform.position; _wasRot = _cam.transform.rotation; _wasFov = _cam.fieldOfView;
            var e = _cam.transform.eulerAngles;
            _yaw = e.y; _pitch = e.x > 180f ? e.x - 360f : e.x;
            // Всё, что водит камеру само (RTS_Camera и подобные), — молчит, пока снимаем.
            _paused.Clear();
            foreach (var b in _cam.GetComponents<Behaviour>())
                if (b != null && b.enabled && !(b is Camera) && !(b is AudioListener) && b.GetType().Name.Contains("Camera"))
                { b.enabled = false; _paused.Add(b); }
            _cleanBefore = CaptureMode.CleanFrame;
            if (!_cleanBefore) CaptureMode.ToggleClean();
            _helpUntil = Time.unscaledTime + 6f;
        }

        private void Off()
        {
            Active = false;
            _playing = false; _blendT = -1f;
            if (_cam != null) { _cam.transform.SetPositionAndRotation(_wasPos, _wasRot); _cam.fieldOfView = _wasFov; }
            foreach (var b in _paused) if (b != null) b.enabled = true;
            _paused.Clear();
            if (CaptureMode.CleanFrame && !_cleanBefore) CaptureMode.ToggleClean();
            if (_timeStep != 0) { _timeStep = 0; Time.timeScale = 1f; }
        }

        // ---------------------------------------------------------- полёт

        private void Fly(bool ctrl)
        {
            float dt = Time.unscaledDeltaTime;
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxisRaw("Mouse X") * 2.4f;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * 2.4f, -89f, 89f);
            }
            _cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            float speed = 6f * (Input.GetKey(KeyCode.LeftShift) ? 4f : 1f) * (ctrl ? 0.25f : 1f);
            var t = _cam.transform;
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += t.forward;
            if (Input.GetKey(KeyCode.S)) move -= t.forward;
            if (Input.GetKey(KeyCode.D)) move += t.right;
            if (Input.GetKey(KeyCode.A)) move -= t.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
            t.position += move * speed * dt;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - wheel * 2f, 12f, 100f);
        }

        private void Step()
        {
            _timeStep = (_timeStep + 1) % 3;
            Time.timeScale = _timeStep switch { 1 => 0.25f, 2 => 0f, _ => 1f };
        }

        // ---------------------------------------------------------- камеры

        private void Save(int slot)
        {
            var t = _cam.transform;
            PlayerPrefs.SetString(Key + slot, string.Join(";", new[]
            {
                t.position.x, t.position.y, t.position.z, t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w, _cam.fieldOfView,
            }.Select3()));
            PlayerPrefs.Save();
            _helpUntil = Time.unscaledTime + 2f;
            _note = Loc.F("Камера {0} запомнена", slot);
        }

        private bool Load(int slot, out Vector3 pos, out Quaternion rot, out float fov)
        {
            pos = default; rot = Quaternion.identity; fov = 60f;
            var s = PlayerPrefs.GetString(Key + slot, "");
            var p = s.Split(';');
            if (p.Length != 8) return false;
            var f = new float[8];
            for (int i = 0; i < 8; i++)
                if (!float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f[i])) return false;
            pos = new Vector3(f[0], f[1], f[2]); rot = new Quaternion(f[3], f[4], f[5], f[6]); fov = f[7];
            return true;
        }

        private void BlendTo(int slot)
        {
            if (!Load(slot, out var pos, out var rot, out var fov)) { _note = Loc.F("Камеры {0} нет — Ctrl+{0}, чтобы запомнить", slot); _helpUntil = Time.unscaledTime + 2f; return; }
            _blendFromPos = _cam.transform.position; _blendFromRot = _cam.transform.rotation; _blendFromFov = _cam.fieldOfView;
            _blendToPos = pos; _blendToRot = rot; _blendToFov = fov;
            _blendT = 0f;
        }

        private void Blending()
        {
            _blendT += Time.unscaledDeltaTime / 1.5f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_blendT));
            _cam.transform.SetPositionAndRotation(Vector3.Lerp(_blendFromPos, _blendToPos, k), Quaternion.Slerp(_blendFromRot, _blendToRot, k));
            _cam.fieldOfView = Mathf.Lerp(_blendFromFov, _blendToFov, k);
            if (_blendT >= 1f) { _blendT = -1f; SyncAngles(); }
        }

        private void SyncAngles()
        {
            var e = _cam.transform.eulerAngles;
            _yaw = e.y; _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        // ---------------------------------------------------------- проезд

        private void Play()
        {
            _route = new List<int>();
            for (int i = 1; i <= 9; i++) if (Load(i, out _, out _, out _)) _route.Add(i);
            if (_route.Count < 2) { _note = Loc.T("Для проезда запомните хотя бы две камеры (Ctrl+1, Ctrl+2…)"); _helpUntil = Time.unscaledTime + 3f; return; }
            _playT = 0f;
            _playing = true;
        }

        /// <summary>По камерам по порядку: путь — кривой Катмулла — Рома, поворот и угол обзора — плавно.</summary>
        private void Playing()
        {
            _playT += Time.unscaledDeltaTime / _segment;
            int n = _route.Count - 1;
            if (_playT >= n) { _playing = false; SyncAngles(); return; }
            int i = Mathf.FloorToInt(_playT);
            float u = Mathf.SmoothStep(0f, 1f, _playT - i) * 0.35f + (_playT - i) * 0.65f;
            Load(_route[Mathf.Max(0, i - 1)], out var p0, out _, out _);
            Load(_route[i], out var p1, out var r1, out var f1);
            Load(_route[i + 1], out var p2, out var r2, out var f2);
            Load(_route[Mathf.Min(n, i + 2)], out var p3, out _, out _);
            _cam.transform.SetPositionAndRotation(CatmullRom(p0, p1, p2, p3, u), Quaternion.Slerp(r1, r2, Mathf.SmoothStep(0f, 1f, u)));
            _cam.fieldOfView = Mathf.Lerp(f1, f2, Mathf.SmoothStep(0f, 1f, u));
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        // ---------------------------------------------------------- привязка

        // Автор, 29.10: «сбоку снять полёт ворона, чтобы камера была к нему привязана». Камера держит то
        // положение относительно цели, в каком её привязали (смещение — в осях цели по курсу), и смотрит
        // туда же; догоняет плавно — без дрожи от взмахов.
        private Transform _follow;
        private Vector3 _offset;
        private Quaternion _look;

        private void FollowRaven()
        {
            var bird = Object.FindFirstObjectByType<Sinbinder.Gameplay.RavenFlight>();
            if (bird == null) { _note = Loc.T("Ворона в сцене нет"); _helpUntil = Time.unscaledTime + 2f; return; }
            Follow(bird.transform);
        }

        private void FollowUnderMouse()
        {
            if (!Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition), out var hit, 400f)) return;
            var w = hit.collider.GetComponentInParent<Sinbinder.Gameplay.Warrior>();
            Follow(w != null ? w.transform : hit.collider.transform);
        }

        private void Follow(Transform target)
        {
            _follow = target;
            var heading = Heading(target);
            _offset = Quaternion.Inverse(heading) * (_cam.transform.position - target.position);
            _look = Quaternion.Inverse(heading) * _cam.transform.rotation;
            _note = Loc.F("Камера привязана: {0} (R или G — отвязать)", target.name);
            _helpUntil = Time.unscaledTime + 2f;
        }

        private void Unfollow()
        {
            _follow = null;
            SyncAngles();
            _note = Loc.T("Камера отвязана");
            _helpUntil = Time.unscaledTime + 1.5f;
        }

        /// <summary>Курс цели — только поворот вокруг вертикали: птица клюёт и кренится, а камера не качается.</summary>
        private static Quaternion Heading(Transform t)
        {
            var f = t.forward; f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f) : Quaternion.identity;
        }

        private void Following()
        {
            if (_follow == null || !_follow.gameObject.activeInHierarchy) { Unfollow(); return; }
            var heading = Heading(_follow);
            var want = _follow.position + heading * _offset;
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, want, k);
            _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, heading * _look, k);
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - wheel * 2f, 12f, 100f);
        }

        // ---------------------------------------------------------- снимок

        private void Shot()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Screenshots");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "photo-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file, 2);
            _note = Loc.F("Снимок: {0}", Path.GetFileName(file));
            _helpUntil = Time.unscaledTime + 2f;
        }

        // ---------------------------------------------------------- подсказка

        private string _note;

        void OnGUI()
        {
            if (!Active || Time.unscaledTime > _helpUntil) return;
            var box = new Rect(16f, 16f, 600f, _note != null && Time.unscaledTime < _helpUntil - 25f ? 60f : 240f);
            GUILayout.BeginArea(box, GUI.skin.box);
            if (!string.IsNullOrEmpty(_note) && _helpUntil - Time.unscaledTime < 3.5f) GUILayout.Label(_note);
            else
            {
                GUILayout.Label(Loc.T("Фоторежим — F8 выйти"));
                GUILayout.Label(Loc.T("WASD — лететь, Q/E — ниже/выше, Shift — быстрее, Ctrl — медленнее"));
                GUILayout.Label(Loc.T("ПКМ зажата — смотреть, колесо — угол обзора"));
                GUILayout.Label(Loc.T("T — время: обычное / медленно / стоп"));
                GUILayout.Label(Loc.T("Ctrl+1…9 — запомнить камеру, 1…9 — перейти к ней"));
                GUILayout.Label(Loc.T("Пробел — проезд по камерам, [ ] — медленнее / быстрее"));
                GUILayout.Label(Loc.T("L — поднять ворона, R — привязать камеру к ворону, G — к тому, что под мышью"));
                GUILayout.Label(Loc.T("P — снимок, H — подсказка"));
            }
            GUILayout.EndArea();
        }
    }

    internal static class PhotoFormat
    {
        /// <summary>Числа — точкой, без культуры: камера, запомненная в одной локали, читается в другой.</summary>
        public static IEnumerable<string> Select3(this float[] values)
        {
            foreach (var v in values) yield return v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
