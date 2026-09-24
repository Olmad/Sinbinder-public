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

        private const string SecondWaveLine =
            "Карган: «Владыка, они узнали, где наш лагерь. "
          + "Вероятно, от одного из наших. Тяжело это признавать, "
          + "но нам нужно бежать».";

        private static readonly Vector3 FirstWave = new Vector3(0f, 0f, 12f);
        private static readonly Vector3 SecondWave = new Vector3(0f, 0f, 15f);

        // Восток, за внешним кольцом палаток (9.4 м) и до края земли (20 м).
        private static readonly Vector3 Edge = new Vector3(16f, 0f, 0f);
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
        /// Край карты — два столба и холодный свет между ними, как в сцене
        /// набега: игрок обязан видеть, куда бежать.
        /// </summary>
        private static void Escape(Transform root)
        {
            var go = new GameObject("Край карты");
            go.transform.SetParent(root);
            go.transform.position = Edge;

            // Лицом от лагеря: столбы встают поперёк дороги наружу.
            go.transform.rotation = Quaternion.LookRotation(Edge.normalized);

            go.AddComponent<EscapeZone>().Configure(EdgeRadius, openAtStart: false);

            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Столб";
                post.transform.SetParent(go.transform, false);
                post.transform.localPosition = new Vector3(EdgeRadius * 0.55f * side, 1.3f, 0f);
                post.transform.localScale = new Vector3(0.22f, 1.3f, 0.22f);
            }

            var beacon = new GameObject("Свет дороги");
            beacon.transform.SetParent(go.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.58f, 0.72f, 0.95f);
            light.intensity = 2.2f;
            light.range = EdgeRadius * 2.4f;
        }
    }
}
