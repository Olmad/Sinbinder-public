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
                    m.SetColor("_EmissionColor", new Color(1.0f, 0.42f, 0.14f) * 2.2f);
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

        public void Taste()
        {
            Warrior who = null;
            float best = Hear;
            bool cookNear = false;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (w == null || w.IsDead || w is SinbinderPlayer || w.Team != Team.Player || w.Soul == null) continue;
                float d = CampFocus.GroundDistance(w.transform.position, transform.position);
                if (d > Hear) continue;
                bool cook = w.Soul.Trade == Trade.Alchemist;
                // Алхимик рядом — говорит он: это его варево.
                if (cook && !cookNear) { cookNear = true; who = w; best = d; continue; }
                if (!cookNear && d < best) { best = d; who = w; }
            }

            string line = who == null
                ? Loc.T("Похлёбка густая, с костями. Есть можно.")
                : Loc.F("{0}: «{1}»", Loc.Name(who.DisplayName), Says(who));
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(line);
            if (who != null && who.TryGetComponent<WarriorAnimation>(out var body)) body.TalkFor(3f);
        }

        /// <summary>Что скажет тот, кто рядом, — по ремеслу и греху. Без чисел.</summary>
        private static string Says(Warrior w)
        {
            if (w.Soul.Trade == Trade.Alchemist) return Loc.T("Не трогайте котелок, владыка. Это не похлёбка.");
            return w.Soul.Sin switch
            {
                SinType.Gluttony => Loc.T("Сядьте, владыка, похлёбка ещё тёплая. Хуже от неё не станет."),
                SinType.Greed => Loc.T("Мясо в похлёбке — моё, владыка. Я его нашёл."),
                SinType.Envy => Loc.T("Ему положили больше. Всегда ему больше."),
                SinType.Pride => Loc.T("Я из общего котла не ем, владыка."),
                SinType.Wrath => Loc.T("Опять одна вода."),
                SinType.Sloth => Loc.T("Мешайте сами, владыка. Я мешал вчера."),
                _ => Loc.T("Пахнет как дома. Почти."),
            };
        }
    }
}
