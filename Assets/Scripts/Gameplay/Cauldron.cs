// Assets/Scripts/Gameplay/Cauldron.cs
// Перевод: текст через Loc
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Котёл у огня (автор, 27.10: «котла нигде нет, а все его так обсуждают» → «интерактивный»).
    ///
    /// * У котла живут двое по примете (облачная, 26.10): чревоугодник — ест («у котла его
    ///   найдёшь всегда», Толстый Ю), алхимик — варит («варит у огня снадобья», Брат Хальд).
    ///   <see cref="CampLife"/> ставит их сюда вместо круга у огня (<see cref="Fits"/>).
    /// * F — Греховод пробует из котла; ближний отзывается по греху. Алхимик рядом —
    ///   его строка главнее: «Не трогайте котелок, владыка. Это не похлёбка».
    ///
    /// Ничего не даёт и не отнимает — вещь «для души», как граммофон: через неё видно, кто какой.
    /// </summary>
    public class Cauldron : MonoBehaviour
    {
        private const float Reach = 2.0f;
        private const float Hear = 5f;

        public static Cauldron Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Instance = null;

        void Awake()
        {
            Instance = this;

            // Угли под котлом тлеют сами (кадр 27.10: котёл ночью был тёмным пятном) —
            // свечением материала, без лишнего света: огней в лагере и так тридцать восемь.
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m == null || !m.name.StartsWith("Ember") || !m.HasProperty("_EmissionColor")) continue;
                    m.EnableKeyword("_EMISSION");
                    // ×0,7, а не ×2,2: выше порога свечения (1,05) угли горели ярко-жёлтым пятном (автор, 29.10:
                    // «ярко-жёлтый огонь под котлом с похлёбкой»); тлеют тёмно-оранжевым.
                    m.SetColor("_EmissionColor", new Color(1.0f, 0.42f, 0.14f) * 0.7f);
                }
                r.materials = mats;
            }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Котёл рядом с Греховодом. Нет — null.</summary>
        public static Cauldron Nearest()
            => Instance != null && SinbinderPlayer.Exists
               && CampFocus.GroundDistance(SinbinderPlayer.Where, Instance.transform.position) <= Reach
                ? Instance : null;

        /// <summary>Живёт ли этот воин у котла, а не у огня: чревоугодник или алхимик.</summary>
        public static bool Fits(Warrior w, out Vector3 at)
        {
            at = default;
            if (Instance == null || w == null || w.Soul == null) return false;
            bool cook = w.Soul.Trade == Trade.Alchemist;
            if (!cook && w.Soul.Sin != SinType.Gluttony) return false;
            at = Instance.transform.position;
            return true;
        }

        public string Prompt => Loc.T("F — попробовать из котла");

        /// <summary>Сколько раз Греховод пробовал варево — за всю игру (эффект копится: <see cref="Brew"/>).</summary>
        public static int Sips { get; private set; }

        /// <summary>Новая игра — прошлое забыто (зовёт сброс новой игры).</summary>
        public static void Forget() => Sips = 0;

        /// <summary>
        /// F у котла — глоток. Автор, 28.10: «зачем вообще обсуждать похлёбку? ладно Ю и Алхимик. Зачем это
        /// Греховоду? какую-нибудь одну фразу можно. По утверждению Алхимика там не похлёбка — было бы забавно
        /// добавлять какой-нибудь странный эффект при пробе. И чтобы он накапливался с каждым использованием».
        /// Отзываются только двое, кому котёл свой: Хальд (это его варево) и Толстый Ю; прочие молчат. Греховод —
        /// одно слово, при первом глотке.
        /// </summary>
        public void Taste()
        {
            Sips++;
            if (Sips == 1) UI.SpeechBubbles.Say(SinbinderPlayer.Instance, Loc.T("Горько."));
            Brew.Sip(Sips, transform.position);

            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w is SinbinderPlayer || w.Soul == null) continue;
                if (CampFocus.GroundDistance(w.transform.position, transform.position) > Hear) continue;
                string line = w.Soul.Trade == Trade.Alchemist ? Cook(Sips)
                            : w.Soul.Sin == SinType.Gluttony ? Glutton(Sips) : null;
                if (line == null) continue;
                UI.SpeechBubbles.Say(w, line);
                if (w.TryGetComponent<WarriorAnimation>(out var body)) body.TalkFor(3f);
            }
        }

        /// <summary>Хальд — тревожнее с каждым глотком: он знает, что сварил.</summary>
        private static string Cook(int sip) => sip switch
        {
            1 => Loc.T("Владыка! Это не похлёбка."),
            2 => Loc.T("Не пейте больше. Я серьёзно."),
            3 => Loc.T("Голоса тонкие? Пройдёт. Наверное."),
            4 => Loc.T("Ноги сами несут? Это корень. Он такой."),
            5 => Loc.T("Глаза… Я же говорил — не трогайте."),
            _ => Loc.T("Вы тоже его видите? У огня?"),
        };

        /// <summary>Толстый Ю — завидует, чем дальше, тем сильнее.</summary>
        private static string Glutton(int sip) => sip switch
        {
            1 => Loc.T("Мне оставьте!"),
            2 => Loc.T("Вкусно ведь? Вкусно."),
            3 => Loc.T("А мне не плохо. А вам?"),
            _ => Loc.T("Я тоже хочу так светиться."),
        };
    }
}
