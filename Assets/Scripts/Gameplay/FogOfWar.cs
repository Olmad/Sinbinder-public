// Assets/Scripts/Gameplay/FogOfWar.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Туман войны — как в Warcraft 3 (слово автора, 24 сентября):
    /// «Первоначально карта полностью в тумане. Если воин разведал
    /// территорию и ушёл — территория остаётся в сером, и если там
    /// противник, его не видно. У воина есть область зрения».
    ///
    /// Три состояния клетки:
    /// <list type="bullet">
    /// <item><b>не разведано</b> — чёрное;</item>
    /// <item><b>разведано, но сейчас не видно</b> — серое: земля и то, что
    /// на ней стоит, видны, враги — нет;</item>
    /// <item><b>видно сейчас</b> — как есть.</item>
    /// </list>
    ///
    /// Здесь — знание: сетка клеток, кто что видит, и кого из врагов
    /// прятать. Рисует туман проход рендера (<see cref="FogOfWarFeature"/>)
    /// по текстуре, которую эта работа обновляет: он темнит всё — землю,
    /// палатки, реквизит, — как в образце, а не одну землю.
    ///
    /// Ставится сам в каждую сцену с землёй (<see cref="GroundNavMesh"/>):
    /// сцены не пересобирать. Границы карты — границы земли.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        /// <summary>Сколько видит воин. Отряд у костра не видит тех, кто на краю лагеря.</summary>
        public const float Sight = 12f;

        /// <summary>Греховод видит дальше: он тот, кем смотрит игрок.</summary>
        public const float HeroSight = 14f;

        /// <summary>Размер клетки, в метрах. Мельче — дороже, крупнее — видно ступени.</summary>
        private const float Cell = 0.5f;

        /// <summary>Как часто пересчитывать, кто что видит. Каждый кадр не нужно.</summary>
        private const float TickSeconds = 0.1f;

        /// <summary>За сколько секунд клетка светлеет или гаснет — чтобы край зрения не мигал.</summary>
        private const float FadeSeconds = 0.3f;

        /// <summary>Идёт ли туман в этой сцене. Спрашивает проход рендера.</summary>
        public static bool Active => _instance != null;

        private static FogOfWar _instance;

        private static readonly int TexId = Shader.PropertyToID("_FogOfWarTex");
        private static readonly int RectId = Shader.PropertyToID("_FogOfWarRect");

        private Vector2 _min;
        private Vector2 _size;
        private int _w;
        private int _h;

        private bool[] _explored;
        private bool[] _seen;
        private float[] _shownSeen;
        private float[] _shownKnown;
        private Color32[] _pixels;
        private Texture2D _texture;

        private float _nextTick;

        /// <summary>Кто из врагов сейчас показан. Прятать заново каждый такт незачем.</summary>
        private readonly Dictionary<Damageable, bool> _shown = new();

        // ──────────────────────────────────
        // Установка
        // ──────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Listen()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        /// <summary>
        /// Поставить туман, если в сцене есть земля. Нет земли — нет карты,
        /// и туману нечего покрывать (меню, пустые сцены).
        /// </summary>
        private static void Install()
        {
            if (_instance != null) return;

            var ground = Object.FindFirstObjectByType<GroundNavMesh>();
            if (ground == null) return;

            var renderer = ground.GetComponent<Renderer>();
            if (renderer == null) return;

            var go = new GameObject("Туман войны");
            go.AddComponent<FogOfWar>().Build(renderer.bounds);
        }

        private void Build(Bounds bounds)
        {
            _instance = this;

            _min = new Vector2(bounds.min.x, bounds.min.z);
            _size = new Vector2(Mathf.Max(1f, bounds.size.x), Mathf.Max(1f, bounds.size.z));
            _w = Mathf.CeilToInt(_size.x / Cell);
            _h = Mathf.CeilToInt(_size.y / Cell);

            int n = _w * _h;
            _explored = new bool[n];
            _seen = new bool[n];
            _shownSeen = new float[n];
            _shownKnown = new float[n];
            _pixels = new Color32[n];

            _texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false, true)
            {
                name = "Туман войны",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Shader.SetGlobalTexture(TexId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(_min.x, _min.y, _size.x, _size.y));

            // Первый пересчёт сразу и без плавности: сцена не должна
            // открываться чернотой, которая за треть секунды отступает.
            Recount();
            for (int i = 0; i < n; i++)
            {
                _shownSeen[i] = _seen[i] ? 1f : 0f;
                _shownKnown[i] = _explored[i] ? 1f : 0f;
            }
            Paint(0f);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_texture != null) Destroy(_texture);
        }

        // ──────────────────────────────────
        // Кадр
        // ──────────────────────────────────

        void Update()
        {
            if (Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + TickSeconds;
                Recount();
                HideEnemies();
            }

            Paint(Time.unscaledDeltaTime);
        }

        /// <summary>Кто что видит сейчас. Разведанное не забывается.</summary>
        private void Recount()
        {
            System.Array.Clear(_seen, 0, _seen.Length);

            foreach (var eye in Eyes())
            {
                float r = eye is SinbinderPlayer ? HeroSight : Sight;
                Stamp(eye.transform.position, r, seen: true);
            }
        }

        /// <summary>
        /// Глаза игрока: свои живые, Греховод в их числе. Перебежчик
        /// перестаёт быть глазами в тот же такт, когда меняет сторону.
        /// </summary>
        private static IEnumerable<Warrior> Eyes()
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
                if (w != null && !w.IsDead && w.Team == Team.Player) yield return w;
        }

        private void Stamp(Vector3 at, float radius, bool seen)
        {
            int cx = Mathf.FloorToInt((at.x - _min.x) / Cell);
            int cz = Mathf.FloorToInt((at.z - _min.y) / Cell);
            int cr = Mathf.CeilToInt(radius / Cell);
            float r2 = (radius / Cell) * (radius / Cell);

            for (int z = Mathf.Max(0, cz - cr); z <= Mathf.Min(_h - 1, cz + cr); z++)
            {
                int dz = z - cz;
                for (int x = Mathf.Max(0, cx - cr); x <= Mathf.Min(_w - 1, cx + cr); x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dz * dz > r2) continue;

                    int i = z * _w + x;
                    _explored[i] = true;
                    if (seen) _seen[i] = true;
                }
            }
        }

        /// <summary>Плавно подвести показанное к знанию и отдать текстуре.</summary>
        private void Paint(float dt)
        {
            float step = FadeSeconds <= 0f ? 1f : dt / FadeSeconds;
            bool instant = dt <= 0f;

            for (int i = 0; i < _pixels.Length; i++)
            {
                float seen = _seen[i] ? 1f : 0f;
                float known = _explored[i] ? 1f : 0f;

                _shownSeen[i] = instant ? seen : Mathf.MoveTowards(_shownSeen[i], seen, step);
                _shownKnown[i] = instant ? known : Mathf.MoveTowards(_shownKnown[i], known, step);

                _pixels[i] = new Color32((byte)(_shownSeen[i] * 255f),
                                         (byte)(_shownKnown[i] * 255f), 0, 255);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        // ──────────────────────────────────
        // Враги
        // ──────────────────────────────────

        private bool SeenAt(Vector3 at)
        {
            int x = Mathf.FloorToInt((at.x - _min.x) / Cell);
            int z = Mathf.FloorToInt((at.z - _min.y) / Cell);
            if (x < 0 || z < 0 || x >= _w || z >= _h) return false;
            return _seen[z * _w + x];
        }

        /// <summary>
        /// Врага вне зрения не видно — ни тела, ни полоски над головой,
        /// ни вспышки над ним; и не выделить, и не навести подсказку:
        /// коллайдер спрятан вместе с ним. Выделенный — теряет выделение.
        /// </summary>
        private void HideEnemies()
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.Team != Team.Enemy) continue;

                var body = w.GetComponent<Damageable>();
                if (body == null) continue;

                // Павший остаётся таким, каким его видели в последний раз.
                if (body.IsDead) continue;

                bool show = SeenAt(w.transform.position);

                // Новый враг и так показан: включать ему то, что кто-то мог
                // выключить нарочно, незачем. Трогаем только смену.
                if (!_shown.TryGetValue(body, out bool was)) was = true;
                _shown[body] = show;
                if (was == show) continue;

                Show(w.gameObject, show);

                if (!show) SelectionManager.Instance?.Drop(w.GetComponent<SelectionComponent>());
            }
        }

        private static void Show(GameObject who, bool show)
        {
            foreach (var r in who.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
            foreach (var c in who.GetComponentsInChildren<Canvas>(true)) c.enabled = show;
            foreach (var c in who.GetComponentsInChildren<Collider>(true)) c.enabled = show;
            foreach (var l in who.GetComponentsInChildren<Light>(true)) l.enabled = show;
        }

        // ──────────────────────────────────
        // Для остальных
        // ──────────────────────────────────

        /// <summary>
        /// Скрыт ли воин туманом от игрока. Спрашивают те, кто иначе выдал
        /// бы спрятанного врага: подпись над головой, наезд камеры.
        /// </summary>
        public static bool Hides(Warrior warrior)
        {
            if (_instance == null || warrior == null) return false;
            if (warrior.Team != Team.Enemy) return false;
            return !_instance.SeenAt(warrior.transform.position);
        }

        /// <summary>
        /// Открыть место серым: разведано, хоть и не видно. Зовёт край карты,
        /// открываясь, — бежать надо туда, где игрок, может, ещё не бывал,
        /// и дорога обязана быть видна.
        /// </summary>
        public static void Reveal(Vector3 at, float radius)
        {
            if (_instance == null) return;
            _instance.Stamp(at, radius, seen: false);
        }
    }
}
