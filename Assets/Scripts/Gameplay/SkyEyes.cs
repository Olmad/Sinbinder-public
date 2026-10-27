// Assets/Scripts/Gameplay/SkyEyes.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Глаза в небе (автор, 27.10: «большие белые глаза, которые будут появляться в небе
    /// и медленно пропадать, если игрок смотрит. И через время снова. Загадка, которую
    /// не каждый увидит и абсолютно никто не поймёт. Удерживает интерес игрока»).
    ///
    /// Нигде о них ни слова: ни подсказки, ни журнала, ни приметы. Видны только глазами
    /// Греховода — сверху неба нет.
    ///
    /// Ход — по часам сцены, без жребия:
    /// * не раньше двух минут в сцене; дальше — раз в четыре минуты, каждый раз в другой
    ///   стороне неба (золотой угол по номеру появления);
    /// * проступают за шесть секунд, изредка моргают;
    /// * посмотрел на них — гаснут медленно, за восемь секунд, будто их застали;
    /// * не посмотрел — через минуту уходят сами.
    ///
    /// Гаснут цветом, а не прозрачностью: краска ведётся к цвету ночного тумана — у этого
    /// материала край прозрачности в сцене не работал (луна вышла квадратом, 27.10).
    /// Кроны ближе 80 м их закрывают — глаза смотрят из-за леса.
    /// </summary>
    public class SkyEyes : MonoBehaviour
    {
        private const float Distance = 80f;
        private const float FirstAfter = 120f;
        private const float Every = 240f;
        private const float FadeIn = 6f, FadeSeen = 8f, FadeAway = 10f, Stay = 60f;
        private const float LookCone = 22f;

        private enum Phase { Waiting, Rising, Watching, Leaving }

        private Phase _phase = Phase.Waiting;
        private float _since, _nextAt, _level, _from;
        private int _count;
        private Vector3 _dir;
        private bool _seen;

        private Transform _root;
        private Material _white, _pupil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void First()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Install();
        }

        private static void OnScene(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            if (Object.FindFirstObjectByType<SkyEyes>() != null) return;
            // Только ночные сцены с луной: «Холодный свет» есть в лагере и склепе.
            if (GameObject.Find("Холодный свет") == null) return;
            new GameObject("Небо смотрит").AddComponent<SkyEyes>();
        }

        void Start()
        {
            _nextAt = Time.time + FirstAfter;
            _root = new GameObject("Пара").transform;
            _root.SetParent(transform, false);

            var shader = Shader.Find("Sprites/Default");
            _white = new Material(shader) { name = "Sky Eye" };
            _pupil = new Material(shader) { name = "Sky Pupil" };
            _white.renderQueue = _pupil.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 20;

            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Shape("Глаз", Almond(40), _white, new Vector3(side * 5.5f, 0f, 0f), new Vector3(7f, 3.2f, 1f));
                // Зрачок — узкая вертикальная щель, чуть ближе к смотрящему.
                Shape("Зрачок", Almond(24), _pupil, new Vector3(side * 5.5f, 0f, 0.05f), new Vector3(0.9f, 2.9f, 1f))
                    .SetParent(eye.parent, true);
            }
            Paint(0f);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float t = Time.time;

            switch (_phase)
            {
                case Phase.Waiting:
                    if (t < _nextAt) return;
                    _count++;
                    // Новая сторона неба — золотой угол по номеру; высота — 18…32° по нему же.
                    float yaw = (_count * 137.5f) % 360f;
                    float pitch = 18f + (_count * 53 % 15);
                    _dir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                    _phase = Phase.Rising; _since = t; _seen = false;
                    break;

                case Phase.Rising:
                    _level = Mathf.Clamp01((t - _since) / FadeIn);
                    if (_level >= 1f) { _phase = Phase.Watching; _since = t; }
                    break;

                case Phase.Watching:
                    if (Looked(cam)) { _seen = true; Leave(t); }
                    else if (t - _since > Stay) Leave(t);
                    break;

                case Phase.Leaving:
                    _level = Mathf.Clamp01(_from * (1f - (t - _since) / (_seen ? FadeSeen : FadeAway)));
                    if (_level <= 0f) { _phase = Phase.Waiting; _nextAt = t + Every; }
                    break;
            }

            var at = cam.transform.position + _dir * Distance;
            _root.SetPositionAndRotation(at, Quaternion.LookRotation(-_dir, Vector3.up));

            // Моргают изредка: раз в семь секунд на долю секунды веки сходятся.
            float blink = Mathf.Repeat(t + _count * 3.1f, 7f) < 0.18f ? 0.12f : 1f;
            _root.localScale = new Vector3(1f, blink, 1f);

            Paint(_phase == Phase.Waiting ? 0f : _level);
        }

        /// <summary>Позвать сейчас (прогон: кадр глаз). В игре не зовётся — у них свои часы.</summary>
        public static void Summon()
        {
            var eyes = Object.FindFirstObjectByType<SkyEyes>();
            if (eyes != null && eyes._phase == Phase.Waiting) eyes._nextAt = 0f;
        }

        /// <summary>Куда смотреть, чтобы их увидеть; ноль — их нет.</summary>
        public static Vector3 Where()
        {
            var eyes = Object.FindFirstObjectByType<SkyEyes>();
            return eyes != null && eyes._phase != Phase.Waiting ? eyes._dir : Vector3.zero;
        }
        private void Leave(float t)
        {
            _phase = Phase.Leaving;
            _since = t;
            _from = _level;
        }

        /// <summary>Смотрит ли Греховод на них — глазами и в их сторону.</summary>
        private bool Looked(Camera cam)
        {
            var view = cam.GetComponent<RTS_Camera>();
            if (view == null || !view.FirstPersonNow) return false;
            return Vector3.Angle(cam.transform.forward, _dir) < LookCone;
        }

        /// <summary>Сила проявления: 0 — цвет ночного неба, 1 — белые глаза с тёмной щелью.</summary>
        private void Paint(float k)
        {
            var sky = RenderSettings.fogColor;
            _white.color = Color.Lerp(sky, new Color(0.95f, 0.96f, 1.0f), k);
            _pupil.color = Color.Lerp(sky, new Color(0.02f, 0.02f, 0.03f), k);
            if (_root != null) _root.gameObject.SetActive(k > 0.001f);
        }

        private Transform Shape(string name, Mesh mesh, Material mat, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var holder = new GameObject(name + " — место").transform;
            holder.SetParent(_root, false);
            holder.localPosition = at;
            go.transform.SetParent(holder, false);
            go.transform.localScale = size;
            return go.transform;
        }

        /// <summary>Миндаль — глаз: две дуги, сходящиеся в уголках. Ширина и высота по 1.</summary>
        private static Mesh Almond(int segs)
        {
            var v = new Vector3[segs * 2 + 1];
            var tri = new int[segs * 2 * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < segs * 2; i++)
            {
                float a = Mathf.PI * 2f * i / (segs * 2);
                float x = Mathf.Cos(a) * 0.5f;
                // Миндаль: высота к уголкам сходит на нет быстрее, чем у эллипса.
                float y = Mathf.Sin(a) * 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.35f);
                v[i + 1] = new Vector3(x, y, 0f);
                tri[i * 3] = 0;
                tri[i * 3 + 1] = (i + 1) % (segs * 2) + 1;
                tri[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "Almond", vertices = v, triangles = tri };
            m.RecalculateBounds();
            return m;
        }
    }
}
