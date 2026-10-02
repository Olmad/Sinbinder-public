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
    /// <b>Цена (2 октября).</b> До этого дня туман перерисовывал всю карту
    /// каждый кадр: девяносто тысяч клеток лагеря, два прохода и отправка
    /// текстуры на видеокарту — и в тихом лагере, где не шевелится никто,
    /// и сотни раз в секунду без потолка кадров. Автор: «Игра очень
    /// нагружает ПК». Теперь перерисовывается только место, где туман
    /// сдвинулся, не чаще тридцати раз в секунду; устоялся — не рисуется
    /// вовсе.
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

        /// <summary>
        /// Насколько карта тумана шире земли, в метрах с каждой стороны.
        /// Столько, чтобы её край не попал в кадр: камера стоит
        /// в двадцати двух метрах и видит заметно дальше края земли.
        /// </summary>
        private const float Margin = 30f;

        /// <summary>Как часто пересчитывать, кто что видит. Каждый кадр не нужно.</summary>
        private const float TickSeconds = 0.1f;

        /// <summary>
        /// Как часто перерисовывать, пока туман движется. Край тумана
        /// мягкий и плывёт треть секунды — тридцати раз хватает глазу.
        /// </summary>
        private const float PaintSeconds = 1f / 30f;

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
        private bool[] _seenBefore;
        private float[] _shownSeen;
        private float[] _shownKnown;
        private Color32[] _pixels;
        private Texture2D _texture;

        private float _nextTick;
        private float _paintedAt;

        /// <summary>
        /// Прямоугольник клеток, края включительно. Пустой — когда начало
        /// больше конца.
        /// </summary>
        private struct Area
        {
            public int X0, Z0, X1, Z1;

            public static Area None => new Area
            {
                X0 = int.MaxValue, Z0 = int.MaxValue, X1 = int.MinValue, Z1 = int.MinValue,
            };

            public bool Empty => X0 > X1 || Z0 > Z1;

            public void Add(int x0, int z0, int x1, int z1)
            {
                if (x0 < X0) X0 = x0;
                if (z0 < Z0) Z0 = z0;
                if (x1 > X1) X1 = x1;
                if (z1 > Z1) Z1 = z1;
            }

            public void Add(Area other)
            {
                if (!other.Empty) Add(other.X0, other.Z0, other.X1, other.Z1);
            }
        }

        /// <summary>Где глаза смотрели в этот такт и в прошлый: туман мог сдвинуться только там.</summary>
        private Area _looked = Area.None;
        private Area _lookedBefore = Area.None;

        /// <summary>Что перерисовать: знание сменилось, а показ его ещё не догнал.</summary>
        private Area _dirty = Area.None;

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

            // Карта тумана шире земли. Иначе её край попадает в кадр:
            // за пределами карты шейдер считает точку неразведанной,
            // и на снимке набега 24 сентября светлая разведанная часть
            // обрывалась ровным прямоугольником по границе земли.
            // С запасом граница проходит там, где и так темно.
            bounds.Expand(new Vector3(Margin * 2f, 0f, Margin * 2f));

            _min = new Vector2(bounds.min.x, bounds.min.z);
            _size = new Vector2(Mathf.Max(1f, bounds.size.x), Mathf.Max(1f, bounds.size.z));
            _w = Mathf.CeilToInt(_size.x / Cell);
            _h = Mathf.CeilToInt(_size.y / Cell);

            int n = _w * _h;
            _explored = new bool[n];
            _seen = new bool[n];
            _seenBefore = new bool[n];
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

            // Первый пересчёт сразу и без плавности, всей картой: сцена
            // не должна открываться чернотой, которая за треть секунды
            // отступает.
            Recount(Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None));
            _dirty = Area.None;
            _dirty.Add(0, 0, _w - 1, _h - 1);
            Paint(0f);
            _paintedAt = Time.unscaledTime;
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
            float now = Time.unscaledTime;

            if (now >= _nextTick)
            {
                _nextTick = now + TickSeconds;

                // Один поиск на такт — и глазам, и врагам.
                var all = Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None);
                Recount(all);
                HideEnemies(all);
            }

            // Рисовать — только когда есть что, и не чаще тридцати раз
            // в секунду. Шаг плавности — по времени с прошлого рисунка,
            // но не больше такта: после долгой тишины свежий край
            // не прыгает, а плывёт, как прежде.
            if (!_dirty.Empty && now - _paintedAt >= PaintSeconds)
            {
                Paint(Mathf.Min(now - _paintedAt, TickSeconds));
                _paintedAt = now;
            }
        }

        /// <summary>
        /// Кто что видит сейчас. Разведанное не забывается. Где видимое
        /// сменилось — туда и перерисовка; не сменилось нигде — рисовать
        /// нечего.
        /// </summary>
        private void Recount(Warrior[] all)
        {
            (_seen, _seenBefore) = (_seenBefore, _seen);
            System.Array.Clear(_seen, 0, _seen.Length);

            _lookedBefore = _looked;
            _looked = Area.None;

            // Глаза игрока: свои живые, Греховод в их числе. Перебежчик
            // перестаёт быть глазами в тот же такт, когда меняет сторону.
            foreach (var w in all)
            {
                if (w == null || w.IsDead || w.Team != Team.Player) continue;
                float r = w is SinbinderPlayer ? HeroSight : Sight;
                Stamp(w.transform.position, r, seen: true);
            }

            // Сменилось только там, куда смотрели сейчас или прошлым тактом.
            var where = _looked;
            where.Add(_lookedBefore);
            if (where.Empty) return;

            for (int z = where.Z0; z <= where.Z1; z++)
            for (int x = where.X0; x <= where.X1; x++)
            {
                int i = z * _w + x;
                if (_seen[i] != _seenBefore[i]) _dirty.Add(x, z, x, z);
            }
        }

        private void Stamp(Vector3 at, float radius, bool seen)
        {
            int cx = Mathf.FloorToInt((at.x - _min.x) / Cell);
            int cz = Mathf.FloorToInt((at.z - _min.y) / Cell);
            int cr = Mathf.CeilToInt(radius / Cell);
            float r2 = (radius / Cell) * (radius / Cell);

            int x0 = Mathf.Max(0, cx - cr), x1 = Mathf.Min(_w - 1, cx + cr);
            int z0 = Mathf.Max(0, cz - cr), z1 = Mathf.Min(_h - 1, cz + cr);
            if (x0 > x1 || z0 > z1) return;

            // Глаз отмечает, куда смотрел (сравнят потом); открытое серым
            // сразу идёт в перерисовку — видимое оно не меняет.
            if (seen) _looked.Add(x0, z0, x1, z1);
            else _dirty.Add(x0, z0, x1, z1);

            for (int z = z0; z <= z1; z++)
            {
                int dz = z - cz;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dz * dz > r2) continue;

                    int i = z * _w + x;
                    _explored[i] = true;
                    if (seen) _seen[i] = true;
                }
            }
        }

        /// <summary>
        /// Плавно подвести показанное к знанию и отдать текстуре — только
        /// там, где туман сдвинулся. Клетка, не дошедшая до своего,
        /// остаётся в перерисовке на следующий раз.
        /// </summary>
        private void Paint(float dt)
        {
            var area = _dirty;
            _dirty = Area.None;
            if (area.Empty) return;

            float step = FadeSeconds <= 0f ? 1f : dt / FadeSeconds;
            bool instant = dt <= 0f;

            for (int z = area.Z0; z <= area.Z1; z++)
            for (int x = area.X0; x <= area.X1; x++)
            {
                int i = z * _w + x;
                float seen = _seen[i] ? 1f : 0f;
                float known = _explored[i] ? 1f : 0f;

                float s = instant ? seen : Mathf.MoveTowards(_shownSeen[i], seen, step);
                float k = instant ? known : Mathf.MoveTowards(_shownKnown[i], known, step);
                _shownSeen[i] = s;
                _shownKnown[i] = k;

                if (s != seen || k != known) _dirty.Add(x, z, x, z);
            }

            // Вторым проходом, а не в том же: сглаживание смотрит
            // на соседей, а они в первом проходе ещё не досчитаны —
            // половина клетки была бы из этого кадра, половина из прошлого.
            // На клетку шире: сдвинутая клетка меняет и соседей.
            int px0 = Mathf.Max(0, area.X0 - 1), px1 = Mathf.Min(_w - 1, area.X1 + 1);
            int pz0 = Mathf.Max(0, area.Z0 - 1), pz1 = Mathf.Min(_h - 1, area.Z1 + 1);
            for (int z = pz0; z <= pz1; z++)
            for (int x = px0; x <= px1; x++)
            {
                int i = z * _w + x;
                _pixels[i] = new Color32((byte)(Smooth(_shownSeen, x, z) * 255f),
                                         (byte)(Smooth(_shownKnown, x, z) * 255f), 0, 255);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        /// <summary>
        /// Среднее по кресту: клетка и четыре соседа, своя — вчетверо
        /// весомее.
        ///
        /// Зачем. Клетка знает только «видно» или «не видно», и край
        /// зрения выходит ступенчатым: на снимке набега 24 сентября
        /// граница тумана шла зубцами по полметра. Сглаживание
        /// не трогает <b>знание</b> — кого прятать, решает по-прежнему
        /// клетка, — оно трогает только то, как это знание нарисовано.
        ///
        /// За краем карты берём своё значение, а не ноль: иначе туман
        /// темнел бы рамкой по всей границе земли.
        /// </summary>
        private float Smooth(float[] map, int x, int z)
        {
            int i = z * _w + x;
            float self = map[i];

            float sum = self * 4f;
            sum += x > 0 ? map[i - 1] : self;
            sum += x < _w - 1 ? map[i + 1] : self;
            sum += z > 0 ? map[i - _w] : self;
            sum += z < _h - 1 ? map[i + _w] : self;

            return sum * 0.125f;
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
        private void HideEnemies(Warrior[] all)
        {
            foreach (var w in all)
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
