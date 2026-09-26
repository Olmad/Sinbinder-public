// Assets/Scripts/Gameplay/RaidEvent.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Разгром лагеря — событие той же сцены, а не новая.
    ///
    /// Слово автора, 24 сентября: «Я думаю, стоит сделать вторую сцену
    /// событием первой. Чтобы Греховода не отбрасывало на респаун. И всё
    /// сохраняло своё положение. И звучит логично». До этого тревога шара
    /// грузила <c>Prologue_Raid</c> — копию того же лагеря, — и Греховод
    /// оказывался у палатки на холме, отряд — снова кругом у костра,
    /// сундук и стол исчезали. Разгром, ради которого место и узнаётся,
    /// начинался с того, что место подменили.
    ///
    /// Здесь ставится ровно то, что сборщик ставил в сцену набега: строка
    /// на чёрном, отъезд камеры, рог, две волны охотников, край карты.
    /// Числа те же, что в <c>DemoSceneBuilder.BuildRaid</c>, кроме края:
    /// в сцене набега он стоял на юге, за холмом, в двадцати пяти метрах,
    /// а в лагере юг закрыт частоколом (дуга в четырнадцать метров),
    /// и земля кончается на двадцати. Край — на востоке, за палатками.
    ///
    /// Отдельной сцены набега больше нет (удалена 24 сентября: автор считал,
    /// что сцен две — лагерь и склеп, — и так и должно быть). Запись,
    /// сделанная посреди разгрома, помнит долю «набег»
    /// (<see cref="Core.SaveSystem.StagedScene"/>); загрузка открывает лагерь,
    /// и разгром разворачивается в нём заново (<see cref="Resume"/>).
    /// </summary>
    public class RaidEvent : MonoBehaviour
    {
        /// <summary>
        /// Имя доли. Сцены с таким именем нет: по нему разгром узнаёт
        /// ведущий лагеря, а запись — что вернуться надо в разгром.
        /// </summary>
        public const string SceneName = "Prologue_Raid";

        /// <summary>Где разгром разворачивается на месте.</summary>
        private const string CampScene = "Prologue_Camp";

        /// <summary>Куда ведёт побег — та же следующая доля, что у сцены набега.</summary>
        private const string After = "Crypt_Entrance";

        private const string Title = "Лагерь знали не только свои.";

        // «Куда» — в самой реплике: без него игрок слышал «нужно бежать»
        // и искал выход по всему лагерю (автор, 26 сентября).
        private const string SecondWaveLine =
            "Карган: «Владыка, они узнали, где наш лагерь. "
          + "Вероятно, от одного из наших. Тяжело это признавать, "
          + "но нам нужно бежать. К восточным воротам — туда, где огни».";

        private static readonly Vector3 FirstWave = new Vector3(0f, 0f, 12f);
        private static readonly Vector3 SecondWave = new Vector3(0f, 0f, 15f);

        // Ворота — на востоке, за внешним кольцом палаток (9.4 м), в проходе.
        // Прежде край стоял ровно на восток, на (16, 0, 0), — а на этой линии
        // во внешнем кольце палатка: из лагеря её было не видно за ней.
        // Восемьдесят градусов от севера — луч, что проходит в прорехи
        // обоих колец (внутреннее — палатки на 65° и 98°, внешнее — на 64°
        // и 90°): ворота видны от костра, дорога к ним прямая. Прошёл ли
        // навмеш — проверяет прогон демо шагом «набег: отряд у края».
        private static readonly Vector3 Edge = new Vector3(13.8f, 0f, 2.4f);
        private const float EdgeRadius = 5f;

        /// <summary>
        /// Идёт ли разгром в лагере. Спрашивают прогон демо и то, что в лагере
        /// начинается само (открытие, сбор, шар, строка на чёрном): посреди
        /// разгрома, открытого загрузкой, им молчать.
        ///
        /// Разгром помнит сцену, в которой развёрнут, а не просто «да»:
        /// сменили сцену — и он кончился, в каком бы порядке Unity ни
        /// выгружала старую. Флаг, который снимал бы OnDestroy, мог бы
        /// дожить до Start нового лагеря и заглушить его открытие.
        /// </summary>
        public static bool Running =>
            _staged && _stagedIn == SceneManager.GetActiveScene();

        private static bool _staged;
        private static Scene _stagedIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _staged = false;

        /// <summary>
        /// Какую сцену открыть ради доли. Доля «набег» своей сцены не имеет —
        /// её открывает лагерь; остальные доли и есть сцены.
        /// </summary>
        public static string HostOf(string part) => part == SceneName ? CampScene : part;

        /// <summary>
        /// Развернуть разгром на месте. Ложь — ведущий не в лагере, и тогда
        /// он грузит следующую долю сам.
        /// </summary>
        public static bool Stage(PrologueDirector director)
        {
            if (director == null) return false;

            // Сцена ведущего, а не открытая: при загрузке записи разгром
            // ставится из sceneLoaded, и так он не зависит от того, успела ли
            // Unity сделать новый лагерь открытой сценой.
            var scene = director.gameObject.scene;
            if (scene.name != CampScene) return false;
            if (_staged && _stagedIn == scene) return true;

            _staged = true;
            _stagedIn = scene;
            Core.SaveSystem.StagedScene = SceneName;

            // Разгром — своя доля: «С начала доли» вернёт в его начало,
            // а не к совету. Состав сперва сверяется с живыми: запись отряда
            // обновляется только при уходе из сцены, и без сверки отметка
            // взяла бы его таким, каким он пришёл в лагерь, — без вещей,
            // отданных у костра, а мешок уже без них.
            var spawner = Object.FindFirstObjectByType<PrologueCampSpawner>();
            if (spawner != null) SquadRoster.Remember(spawner.GetComponentsInChildren<Warrior>());
            Core.SaveSystem.MarkCheckpoint();

            // Ведущий лагеря становится ведущим набега до того, как что-то
            // появится: иначе его страховки лагеря успели бы сработать.
            director.BecomeRaid(After);

            var go = new GameObject("Разгром");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<RaidEvent>().StartCoroutine(Unfold(go.transform));
            return true;
        }

        /// <summary>
        /// Вернуться в разгром по записи, сделанной посреди него. Зовёт
        /// <see cref="Core.SaveSystem"/>, когда лагерь открыт загрузкой, —
        /// до Start сцены, чтобы шар, совет и открытие лагеря успели увидеть,
        /// что идёт разгром.
        ///
        /// Разгром начинается сначала: строка на чёрном, обе волны, край.
        /// Середины боя запись не хранит (<see cref="Core.SaveSystem"/>),
        /// и отдельная сцена набега, пока она была, делала то же самое.
        /// </summary>
        public static void Resume(Scene scene)
        {
            // Ищем в открытой сцене, а не где попало: в sceneLoaded старая
            // сцена может быть ещё не выгружена, и её ведущий нашёлся бы
            // первым — а он склепа или прошлого лагеря.
            PrologueDirector director = null;
            foreach (var d in Object.FindObjectsByType<PrologueDirector>(FindObjectsSortMode.None))
                if (d.gameObject.scene == scene) { director = d; break; }

            if (Stage(director))
            {
                Debug.Log("[ЗАПИСЬ] Запись посреди разгрома: разгром развёрнут в лагере заново.");
                return;
            }

            Debug.LogWarning("[ЗАПИСЬ] Запись сделана посреди разгрома, но в открытой "
                           + "сцене нет ведущего лагеря — разгрому негде развернуться. "
                           + "Пересоберите сцены демо.");
        }

        private static IEnumerator Unfold(Transform root)
        {
            // Кадр на то, чтобы сцена устоялась. По записи разгром ставится
            // из sceneLoaded: старая сцена ещё может быть загружена, и её
            // строка на чёрном или камера нашлись бы вместо своих — строка,
            // уничтоженная посреди показа, заперла бы разгром до конца игры.
            yield return null;

            // Строка на чёрном — та же, что открывала сцену набега.
            // Пока она на экране, мир стоит, и охотники выходят после.
            var title = Object.FindFirstObjectByType<UI.PrologueTitleUI>();
            if (title != null)
            {
                title.Again(Title);
                yield return null;
                while (UI.PrologueTitleUI.Showing) yield return null;
            }
            else
            {
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Title);
            }

            // Отъезд камеры после отказа и рог на приказ «отходить» —
            // по разу за пролог, как было в сцене набега.
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraPullback>() == null)
                cam.gameObject.AddComponent<CameraPullback>();

            var horn = new GameObject("Рог");
            horn.transform.SetParent(root);
            horn.AddComponent<AudioSource>();
            horn.AddComponent<RetreatHorn>();

            // Край ставим раньше волн: вторая открывает его, выходя.
            Escape(root);

            Wave(root, "Охотники", FirstWave, count: 3, width: 5f,
                 afterFieldClear: false, opensEscape: false, announce: "");

            Wave(root, "Охотники: вторая волна", SecondWave, count: 6, width: 10f,
                 afterFieldClear: true, opensEscape: true, announce: SecondWaveLine);

            Debug.Log("[ПРОЛОГ] Разгром развёрнут в лагере.");
        }

        private static void Wave(Transform root, string name, Vector3 at, int count,
            float width, bool afterFieldClear, bool opensEscape, string announce)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root);
            go.transform.position = at;
            go.transform.LookAt(new Vector3(0f, at.y, 0f));

            go.AddComponent<HunterSquadSpawner>()
              .Configure(count, width, afterFieldClear, opensEscape, announce);
        }

        /// <summary>
        /// Ворота наружу — конкретная точка, а не «край где-то на востоке».
        ///
        /// Автор, 26 сентября: «Я не смог выйти из лагеря». Здесь стояли
        /// два тонких столба и холодный свет — за палаткой, в темноте;
        /// с высоты их прятал туман, от первого лица — палатки. Теперь это
        /// ворота: два высоких столба с перекладиной, на каждом факел,
        /// между ними холодный свет дороги. Пока уходить рано, они тёмные;
        /// открывая край, <see cref="EscapeZone.Arm"/> их зажигает и ставит
        /// над ними метку «Выход» (<see cref="UI.ExitMarker"/>).
        ///
        /// Коллайдеров у ворот нет: навмеш уже испечён, обходить их некому,
        /// а луч щелчка «иди сюда» упирался бы в столб, а не в землю.
        /// </summary>
        private static void Escape(Transform root)
        {
            var go = new GameObject("Ворота");
            go.transform.SetParent(root);
            go.transform.position = Edge;

            // Лицом от лагеря: столбы встают поперёк дороги наружу.
            go.transform.rotation = Quaternion.LookRotation(new Vector3(Edge.x, 0f, Edge.z).normalized);

            go.AddComponent<EscapeZone>().Configure(EdgeRadius, openAtStart: false);

            const float half = 1.7f;     // полширины прохода
            const float tall = 3.4f;     // высота столба

            for (int side = -1; side <= 1; side += 2)
            {
                var post = Piece(go.transform, PrimitiveType.Cylinder, "Столб",
                    new Vector3(half * side, tall * 0.5f, 0f), new Vector3(0.28f, tall * 0.5f, 0.28f));

                // Факел — на столбе, тёплый: его видно от костра через прорехи
                // палаток, и он не спорит с холодным светом дороги.
                Piece(go.transform, PrimitiveType.Cylinder, "Факел",
                    new Vector3(half * side, tall + 0.15f, 0f), new Vector3(0.14f, 0.18f, 0.14f));
                Lamp(go.transform, "Огонь факела", new Vector3(half * side, tall + 0.45f, 0f),
                    new Color(1f, 0.62f, 0.3f), 2.4f, 8f);

                post.name = side < 0 ? "Столб левый" : "Столб правый";
            }

            Piece(go.transform, PrimitiveType.Cube, "Перекладина",
                new Vector3(0f, tall - 0.2f, 0f), new Vector3(half * 2f + 0.5f, 0.24f, 0.24f));

            Lamp(go.transform, "Свет дороги", new Vector3(0f, 2.4f, 0f),
                new Color(0.58f, 0.72f, 0.95f), 2.2f, EdgeRadius * 2.4f);
        }

        /// <summary>Кусок ворот — примитив без коллайдера.</summary>
        private static GameObject Piece(Transform parent, PrimitiveType type, string name,
            Vector3 local, Vector3 scale)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            Object.Destroy(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = local;
            piece.transform.localScale = scale;
            return piece;
        }

        /// <summary>Свет ворот. Зажигает его край, когда уходить пора.</summary>
        private static void Lamp(Transform parent, string name, Vector3 local,
            Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }
    }
}
