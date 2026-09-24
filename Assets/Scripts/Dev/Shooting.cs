// Assets/Scripts/Dev/Shooting.cs
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sinbinder.Dev
{
    /// <summary>
    /// Съёмка для роликов: буквы A-O-S в любой сцене, свободная камера
    /// с запоминанием точек, позы буквам и воинам, замерший мир.
    /// Включается командой <c>aos</c> в консоли (<see cref="CheatConsole"/>).
    ///
    /// Автор, 24 сентября: «сцены из игры и буквы A, O, S ходят по сценам
    /// и общаются с персонажами». И следом: «нужно несколько кнопок
    /// управления — переключение анимации, расстановка и переключение
    /// камер».
    ///
    /// <b>Два режима рук.</b> «Съёмка»: клавиши у камеры и актёров, игра
    /// молчит (<see cref="InputHush"/>). «Игра» (F2): руки снова у игры,
    /// буквы стоят где стояли — так снимают настоящий отказ в настоящем
    /// бою, с буквами рядом.
    ///
    /// Реплик у букв нет нарочно: слова приходят с озвучкой и субтитрами
    /// при монтаже. Картинка без надписей годится и русскому ролику,
    /// и английскому.
    ///
    /// Живёт в своей сцене: сменилась сцена — съёмка кончилась.
    /// </summary>
    public class Shooting : MonoBehaviour
    {
        public static bool Active => _instance != null;
        private static Shooting _instance;

        private const float Fly = 6f;
        private const float Look = 2.2f;
        private const float Glide = 1.6f;
        private const int Slots = 9;

        private readonly List<LetterActor> _letters = new();
        private readonly HashSet<Gameplay.WarriorAnimation> _posed = new();

        private Camera _camera;
        private float _yaw, _pitch;
        private float _startFov;

        private bool _shooting = true;
        private bool _froze;
        private bool _cleaned;
        private bool _showKeys = true;

        private LetterActor _letter;
        private Gameplay.Warrior _warrior;

        private bool _gliding;
        private float _glideT;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private float _fromFov, _toFov;

        private GameObject _canvas;
        private Text _keys;
        private Text _status;

        // ──────────────────────────────────
        // Вкл/выкл
        // ──────────────────────────────────

        /// <summary>Включить или выключить. Истина — съёмка включена.</summary>
        public static bool Toggle()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
                return false;
            }

            var go = new GameObject("Съёмка");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            _instance = go.AddComponent<Shooting>();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        void Start()
        {
            _camera = Camera.main;

            if (_camera == null)
            {
                Debug.LogWarning("[СЪЁМКА] Камеры в сцене нет — снимать нечем.");
                Destroy(gameObject);
                return;
            }

            _startFov = _camera.fieldOfView;
            var e = _camera.transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;

            // Буквы — туда, куда смотрит камера, лицом к ней.
            var ray = new Ray(_camera.transform.position, _camera.transform.forward);
            Vector3 spot = Physics.Raycast(ray, out var hit, 200f)
                ? hit.point
                : _camera.transform.position + _camera.transform.forward * 6f;
            _letters.AddRange(LetterActor.Troupe(spot, _camera.transform.position - spot));
            foreach (var l in _letters) SceneManager.MoveGameObjectToScene(l.gameObject, gameObject.scene);

            BuildHelp();
            Shoot(true);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;

            InputHush.Release(this);
            if (_froze) Freeze(false);
            if (_cleaned && CaptureMode.CleanFrame) CaptureMode.ToggleClean();

            foreach (var w in _posed) if (w != null) w.Pose(null);
            foreach (var l in _letters) if (l != null) Destroy(l.gameObject);
            if (_canvas != null) Destroy(_canvas);
            if (_camera != null) _camera.fieldOfView = _startFov;
        }

        // ──────────────────────────────────
        // Кадр
        // ──────────────────────────────────

        void Update()
        {
            if (_camera == null) return;

            // Пока открыта консоль, клавиши принадлежат ей.
            if (CheatConsole.Open) return;

            // F2, а не Tab: в режиме игры Tab листает суму.
            if (Input.GetKeyDown(KeyCode.F2)) Shoot(!_shooting);

            // Эти два нужны и в режиме игры: отказ снимают там.
            if (Input.GetKeyDown(KeyCode.K)) CaptureMode.RefuseNext();
            if (Input.GetKeyDown(KeyCode.P)) Freeze(!_froze);

            if (_shooting)
            {
                Keys();
                Drive();
            }

            Status();
        }

        private void Shoot(bool on)
        {
            _shooting = on;
            if (on) InputHush.Hold(this, menuToo: false);
            else InputHush.Release(this);

            if (on)
            {
                var e = _camera.transform.eulerAngles;
                _yaw = e.y;
                _pitch = e.x > 180f ? e.x - 360f : e.x;
            }
        }

        private void Keys()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { _showKeys = !_showKeys; _keys.enabled = _showKeys; }

            if (Input.GetKeyDown(KeyCode.H))
            {
                CaptureMode.ToggleClean();
                _cleaned = CaptureMode.CleanFrame;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            for (int i = 1; i <= Slots; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + i)) continue;
                if (ctrl) Remember(i);
                else Recall(i, smooth: alt);
            }

            if (Input.GetMouseButtonDown(0)) Pick();

            if (Input.GetKeyDown(KeyCode.M) && _letter != null && Aim(out var point))
            {
                if (shift) _letter.PlaceAt(point);
                else _letter.WalkTo(point);
            }

            if (Input.GetKeyDown(KeyCode.Z)) Turn(-15f);
            if (Input.GetKeyDown(KeyCode.X)) Turn(15f);
            if (Input.GetKeyDown(KeyCode.L)) FaceCamera();
            if (Input.GetKeyDown(KeyCode.T)) Pose(shift ? -1 : 1);
        }

        // ──────────────────────────────────
        // Камера
        // ──────────────────────────────────

        private void Drive()
        {
            float dt = Time.unscaledDeltaTime;
            var t = _camera.transform;

            if (_gliding)
            {
                _glideT += dt / Glide;
                float k = Mathf.SmoothStep(0f, 1f, _glideT);
                t.SetPositionAndRotation(Vector3.Lerp(_fromPos, _toPos, k),
                                         Quaternion.Slerp(_fromRot, _toRot, k));
                _camera.fieldOfView = Mathf.Lerp(_fromFov, _toFov, k);
                if (_glideT >= 1f) { _gliding = false; Shoot(true); }
                return;
            }

            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxisRaw("Mouse X") * Look;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * Look, -85f, 85f);
                t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += t.forward;
            if (Input.GetKey(KeyCode.S)) move -= t.forward;
            if (Input.GetKey(KeyCode.D)) move += t.right;
            if (Input.GetKey(KeyCode.A)) move -= t.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            float speed = Fly * (Input.GetKey(KeyCode.LeftShift) ? 3f : 1f);
            t.position += move * speed * dt;

            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f)
                _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView - wheel * 20f, 12f, 90f);
        }

        private string SlotKey(int i) => $"sinbinder.shot.{gameObject.scene.name}.{i}";

        private void Remember(int i)
        {
            var t = _camera.transform;
            var c = CultureInfo.InvariantCulture;
            string value = string.Join(";",
                t.position.x.ToString(c), t.position.y.ToString(c), t.position.z.ToString(c),
                t.rotation.x.ToString(c), t.rotation.y.ToString(c), t.rotation.z.ToString(c),
                t.rotation.w.ToString(c), _camera.fieldOfView.ToString(c));

            try { PlayerPrefs.SetString(SlotKey(i), value); PlayerPrefs.Save(); }
            catch (System.Exception e) { Debug.LogWarning("[СЪЁМКА] Точку не записать: " + e.Message); }

            Debug.Log($"[СЪЁМКА] Камера {i} запомнена.");
        }

        private void Recall(int i, bool smooth)
        {
            string value;
            try { value = PlayerPrefs.GetString(SlotKey(i), ""); }
            catch { value = ""; }

            var parts = value.Split(';');
            if (parts.Length != 8) { Debug.Log($"[СЪЁМКА] Камера {i} не задана: Ctrl + {i} — запомнить."); return; }

            var c = CultureInfo.InvariantCulture;
            var f = new float[8];
            for (int k = 0; k < 8; k++)
                if (!float.TryParse(parts[k], NumberStyles.Float, c, out f[k])) return;

            var pos = new Vector3(f[0], f[1], f[2]);
            var rot = new Quaternion(f[3], f[4], f[5], f[6]);

            if (smooth)
            {
                _fromPos = _camera.transform.position; _toPos = pos;
                _fromRot = _camera.transform.rotation; _toRot = rot;
                _fromFov = _camera.fieldOfView; _toFov = f[7];
                _glideT = 0f;
                _gliding = true;
                return;
            }

            _camera.transform.SetPositionAndRotation(pos, rot);
            _camera.fieldOfView = f[7];
            Shoot(true);
        }

        // ──────────────────────────────────
        // Актёры
        // ──────────────────────────────────

        private void Pick()
        {
            _letter = null;
            _warrior = null;

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 300f)) return;

            _letter = hit.collider.GetComponentInParent<LetterActor>();
            if (_letter == null) _warrior = hit.collider.GetComponentInParent<Gameplay.Warrior>();
        }

        private bool Aim(out Vector3 point)
        {
            point = default;
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 300f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<LetterActor>() != null) continue;
                point = hit.point;
                return true;
            }
            return false;
        }

        private void Turn(float degrees)
        {
            if (_letter != null) _letter.Turn(degrees);
            else if (_warrior != null) _warrior.transform.Rotate(0f, degrees, 0f, Space.World);
        }

        private void FaceCamera()
        {
            var eye = _camera.transform.position;
            if (_letter != null) _letter.Face(eye);
            else if (_warrior != null)
            {
                var to = eye - _warrior.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                    _warrior.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
        }

        /// <summary>
        /// Следующая поза. У воина — состояния тела из <see cref="Gameplay.BodyMotion"/>
        /// и «сам»: тело снова слушает душу.
        /// </summary>
        private void Pose(int step)
        {
            if (_letter != null) { _letter.Cycle(step); return; }
            if (_warrior == null) return;

            var body = _warrior.GetComponent<Gameplay.WarriorAnimation>();
            if (body == null) return;

            var states = new List<string> { null };
            states.AddRange(Gameplay.BodyMotion.All());

            int now = states.IndexOf(body.Posed);
            string next = states[(now + step + states.Count) % states.Count];

            body.Pose(next);
            if (next != null) _posed.Add(body);
            else _posed.Remove(body);
        }

        private void Freeze(bool on)
        {
            _froze = on;
            var pause = Core.GamePauseController.Instance;

            if (pause != null)
            {
                if (on) pause.Pause();
                else pause.Resume();
                return;
            }

            Time.timeScale = on ? 0f : 1f;
        }

        // ──────────────────────────────────
        // Строки на экране
        // ──────────────────────────────────

        private void Status()
        {
            if (_status == null) return;

            string who = _letter != null
                ? $"{_letter.Title} · {(_letter.Walking ? "идёт" : LetterActor.Word(_letter.Now))}"
                : _warrior != null
                    ? $"{_warrior.name} · {Posed(_warrior)}"
                    : "никто не выбран";

            _status.text = (_shooting ? "СЪЁМКА" : "ИГРА (F2 — к съёмке)")
                         + (_froze ? " · мир стоит" : "")
                         + " · " + who;
        }

        private static string Posed(Gameplay.Warrior w)
        {
            var body = w.GetComponent<Gameplay.WarriorAnimation>();
            return body == null || body.Posed == null ? "сам" : body.Posed;
        }

        private void BuildHelp()
        {
            var font = Fonts.Find();

            _canvas = new GameObject("Холст съёмки", typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(_canvas, gameObject.scene);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _keys = Line("Клавиши", font, 18, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(560f, 520f));
            _keys.alignment = TextAnchor.UpperLeft;
            _keys.text =
                "СЪЁМКА — aos в консоли (~) ещё раз: выйти\n" +
                "W A S D — лететь · Q / E — вниз / вверх · Shift — быстрее\n" +
                "ПКМ зажать — смотреть · колесо — объектив\n" +
                "Ctrl + 1…9 — запомнить камеру · 1…9 — встать · Alt + 1…9 — плавно\n" +
                "ЛКМ — выбрать букву или воина\n" +
                "M — идти к курсору · Shift + M — встать туда сразу\n" +
                "Z / X — повернуть · L — лицом к камере\n" +
                "T / Shift + T — поза: следующая / прежняя\n" +
                "P — остановить мир · H — спрятать интерфейс\n" +
                "K — следующий приказ будет отказом\n" +
                "F2 — руки игре / съёмке · F1 — спрятать этот список";

            _status = Line("Строка", font, 20, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1400f, 32f));
            _status.alignment = TextAnchor.MiddleCenter;
        }

        private Text Line(string name, Font font, int size, Vector2 anchor, Vector2 at, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_canvas.transform, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = at;
            rt.sizeDelta = box;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = new Color(0.95f, 0.93f, 0.86f);
            text.raycastTarget = false;
            go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return text;
        }
    }
}
