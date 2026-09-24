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
    /// Сцена набега не удалена: запись, сделанная посреди разгрома, помнит
    /// долю «набег» (<see cref="Core.SaveSystem.StagedScene"/>), и загрузка
    /// открывает её, как прежде.
    /// </summary>
    public class RaidEvent : MonoBehaviour
    {
        /// <summary>Доля, которую событие заменяет. По этому имени его узнаёт ведущий.</summary>
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

        /// <summary>Идёт ли разгром в лагере. Спрашивает прогон демо.</summary>
        public static bool Running { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Running = false;

        /// <summary>
        /// Развернуть разгром на месте. Ложь — сцена не лагерь (например,
        /// загрузили отдельную сцену набега), и тогда ведущий грузит
        /// следующую долю, как прежде.
        /// </summary>
        public static bool Stage(PrologueDirector director)
        {
            if (director == null) return false;
            if (SceneManager.GetActiveScene().name != CampScene) return false;
            if (Running) return true;

            Running = true;
            Core.SaveSystem.StagedScene = SceneName;

            // Ведущий лагеря становится ведущим набега до того, как что-то
            // появится: иначе его страховки лагеря успели бы сработать.
            director.BecomeRaid(After);

            var go = new GameObject("Разгром");
            go.AddComponent<RaidEvent>().StartCoroutine(Unfold(go.transform));
            return true;
        }

        void OnDestroy()
        {
            // Сцену сменили — разгром кончился вместе с ней.
            Running = false;
        }

        private static IEnumerator Unfold(Transform root)
        {
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
