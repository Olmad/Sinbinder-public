// Assets/Scripts/Gameplay/Whisper.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// «Шепнуть» — искушение как действие игрока (docs/60-TEMPTATION.md; девиз «Не командуй. Искушай.»).
    /// До сих пор искушение было невидимым: предмет с грехом тихо подкручивал очки решения (TemptationResolver),
    /// и ни игрок, ни зритель трейлера не видели, что воинов искушают.
    ///
    /// Воин колеблется или отказал приказу — Z рядом с ним (до 12 м): Греховод шепчет то, чего хочет его грех.
    /// Что шепнуть — решает душа, а не игрок; игрок решает, шептать ли. Цена словом, без цифр: жадному —
    /// монеты из кошеля; остальным — след в душе: поддавшийся шёпоту грех звучит громче навсегда (развращение,
    /// мягко, с потолком). Добродетельному (спектр ниже нуля) шептать бесполезно: «Не купишь».
    ///
    /// Миг — по мерке «Вызова» и шара: мир медленнее, камера на лицо, глаза вспыхивают цветом греха, строка
    /// Греховода и ответ воина. Поддался — приказ исполняется (AOSWarriorWrapper смотрит <see cref="Persuaded"/>).
    /// Ставит себя сам (как привычки): раз на сцену, слушает Z.
    /// </summary>
    public class Whisper : MonoBehaviour
    {
        public const float Reach = 12f;              // слышит шёпот, м
        private const float Holds = 20f;             // сколько держится уговор, с
        private const float Again = 45f;             // одного и того же — не чаще, с
        private const float Corrupts = 4f;           // насколько грех громче после поддавшегося шёпота
        private const float Ceiling = 90f;           // выше — не растёт
        private const int GreedCoins = 3;
        private const float Slow = 0.3f;
        private const float Seconds = 2.4f;

        private struct Consent
        {
            public float Until;
            public uint OrderRevision;
        }
        private static readonly Dictionary<Warrior, Consent> Until = new();
        private static readonly Dictionary<Warrior, float> Last = new();
        private static bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Until.Clear(); Last.Clear(); _busy = false; ShowingOutcome = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<Whisper>() != null) return;
            var go = new GameObject("Шёпот Греховода");
            DontDestroyOnLoad(go);
            go.AddComponent<Whisper>();
        }

        /// <summary>Уговорён ли воин исполнить свой приказ (поддался шёпоту, приказ ещё в силе).</summary>
        public static bool Persuaded(Warrior w)
        {
            if (w == null || !Until.TryGetValue(w, out var consent)) return false;
            if (!AOS.OrderIntegrity.WhisperApplies(
                consent.OrderRevision, w.CommandRevision, w.HasCommand, consent.Until, Time.time))
            {
                Until.Remove(w);
                return false;
            }
            return true;
        }

        /// <summary>Кому сейчас можно шепнуть: свой, жив, колеблется или отказал, рядом с Греховодом.</summary>
        public static bool Can(Warrior w) => Can(w, anyDistance: false);

        /// <summary>
        /// Шепнуть можно было бы, подойди Греховод ближе. Приказ чаще всего отдают
        /// издали, сверху — и подсказка «Z — шепнуть» над отказавшим не появлялась
        /// вовсе: глагол «искушай» оставался ненайденным (аудит 04.11, P0-2).
        /// </summary>
        public static bool CanIfCloser(Warrior w) => !Can(w, anyDistance: false) && Can(w, anyDistance: true);

        private static bool Can(Warrior w, bool anyDistance)
        {
            if (w == null || w.IsDead || w is SinbinderPlayer || w.Team != Team.Player || _busy) return false;
            if (!SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead) return false;
            if (!anyDistance && CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position) > Reach) return false;
            if (Persuaded(w)) return false;
            if (Last.TryGetValue(w, out var at) && Time.time - at < Again) return false;
            var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
            if (mind == null || !w.HasCommand) return false;
            // An old refusal cannot make a newly issued order whisperable.
            // Wait for the real thirteen-voice verdict on this exact revision.
            if (!mind.HasCurrentCommandDecision) return false;
            var d = mind.LastDecisionDetail;
            return d.Hesitated || d.RefusedCommand;
        }

        private static Whisper _one;

        /// <summary>Чем кончился последний шёпот — словом (прогон демо пишет его в отчёт). Сброс — null.</summary>
        public static string LastResult;

        /// <summary>Итог шёпота сейчас на полосе — прогон снимает кадр.</summary>
        public static bool ShowingOutcome { get; private set; }

        void Awake() => _one = this;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Z) && Time.timeScale > 0f) TryAny();
        }

        /// <summary>Шепнуть ближнему к Греховоду из тех, кому можно. Ложь — некому.</summary>
        public static bool TryAny()
        {
            if (_one == null) return false;
            Warrior best = null;
            float near = float.MaxValue;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (!Can(w)) continue;
                float d = CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position);
                if (d < near) { near = d; best = w; }
            }
            if (best == null) return false;
            _one.StartCoroutine(_one.Moment(best));
            return true;
        }

        /// <summary>Что шепчет Греховод греху и что отвечает поддавшийся.</summary>
        private static (string offer, string yes) Words(SinType sin) => sin switch
        {
            SinType.Greed    => (Loc.T("Половина добычи — твоя."), Loc.T("…Половина. Запомню.")),
            SinType.Pride    => (Loc.T("Пусть все видят — первым будешь ты."), Loc.T("Смотрите же.")),
            SinType.Wrath    => (Loc.T("Он смеялся над тобой у огня."), Loc.T("Смеялся?!")),
            SinType.Envy     => (Loc.T("Карган бы смог. А ты?"), Loc.T("Я — не хуже.")),
            SinType.Lust     => (Loc.T("Она смотрит."), Loc.T("…Ну, раз смотрит.")),
            SinType.Gluttony => (Loc.T("После — котёл твой."), Loc.T("Весь котёл.")),
            _                => (Loc.T("Сделаешь — до утра никто не тронет."), Loc.T("До утра. Ладно.")),
        };

        private IEnumerator Moment(Warrior w)
        {
            _busy = true;
            Last[w] = Time.time;
            // The bargain concerns the order that was pending when Z was
            // pressed. A new order is never automatically purchased.
            uint agreedOrder = w.CommandRevision;
            var sin = w.Soul != null ? w.Soul.Sin : SinType.Sloth;
            var (offer, yes) = Words(sin);
            var hero = SinbinderPlayer.Instance;

            // Добродетельного не купишь: спектр его греха ниже нуля.
            bool virtuous = w.Soul != null && w.Soul.Get(sin) <= 0f;
            // Жадному — монеты; пуст кошель — нечем.
            var bag = Inventory.PlayerInventory.Instance;
            bool broke = sin == SinType.Greed && (bag == null || bag.Gold < GreedCoins);
            bool yielded = !virtuous && !broke;
            string answer = virtuous ? Loc.T("Не купишь.") : broke ? Loc.T("Половина чего? Кошель-то пуст.") : yes;

            float before = w.Soul != null ? w.Soul.Get(sin) : Ceiling;
            bool applied = false;
            string outcome = "";
            // Commit only when the soul actually answers, not at the start
            // of the cinematic. The player may cancel the original order
            // during the spoken offer. Such a contract costs nothing.
            void ResolveBargain()
            {
                bool canPay = sin != SinType.Greed || (bag != null && bag.Gold >= GreedCoins);
                applied = yielded && canPay && w != null && !w.IsDead &&
                    w.Soul != null && w.HasCommand && w.CommandRevision == agreedOrder;
                if (applied)
                {
                    if (sin == SinType.Greed) bag.SpendGold(GreedCoins);
                    Until[w] = new Consent { Until = Time.time + Holds, OrderRevision = agreedOrder };
                    if (before < Ceiling) w.Soul.Change(sin, Mathf.Min(Corrupts, Ceiling - before));
                }
                else if (yielded)
                    answer = canPay
                        ? Loc.T("Приказ уже изменён. На другой я не соглашался.")
                        : Loc.T("Кошель уже пуст.");
                outcome = yielded && !applied
                    ? Loc.T("Уговор не состоялся: новый приказ требует нового разговора.")
                    : Outcome(w, sin, applied, virtuous, louder: applied && before < Ceiling);
            }

            bool slowed = Time.timeScale > 0f;
            if (slowed) Time.timeScale = Slow;
            var camera = Dialogue.DialogueCameraController.Instance;
            Coroutine focus = null;
            if (camera != null && !camera.InDialogue)
            {
                camera.SaveCameraPosition();
                focus = StartCoroutine(camera.FocusOn(w.transform));
            }

            // Искушение — на полосе кино, тремя строками: что шепнул Греховод,
            // что ответил воин, чем это кончилось. Аудит 03.11, п. 1 (П0): «событие
            // искушения с ценой, откликом, последствиями» — цена и след в душе
            // прежде списывались молча, а слова висели мелкими пузырями.
            // Щелчок мыши — следующая строка (Letterbox.Hold).
            var box = focus != null ? UI.Letterbox.Instance : null;
            if (box != null)
            {
                box.Say(Loc.T("Греховод, шёпотом"), offer);
                yield return UI.Letterbox.Hold(1.9f);
                ResolveBargain();
                UI.Letterbox.Instance?.Say(Loc.Name(w.DisplayName), answer);
                if (w != null && w.TryGetComponent<Audio.VoiceGenerator>(out var said)) said.Babble(answer, 0.9f);
                if (applied && w != null) StartCoroutine(Flare(w, SinPalette.Of(sin)));
                yield return UI.Letterbox.Hold(1.9f);
                UI.Letterbox.Instance?.Say(null, outcome);
                ShowingOutcome = true;
                yield return UI.Letterbox.Hold(Herald.Hold(outcome));
                ShowingOutcome = false;
            }
            else
            {
                // Кадр занят — по-старому, пузырями над головами.
                UI.SpeechBubbles.Say(hero, offer, 2.6f);
                float t = 0f;
                bool answered = false;
                while (t < Seconds)
                {
                    if (!answered && t >= 1.0f && w != null)
                    {
                        answered = true;
                        ResolveBargain();
                        if (applied) StartCoroutine(Flare(w, SinPalette.Of(sin)));
                        UI.SpeechBubbles.Say(w, answer, 2.4f);
                        if (w.TryGetComponent<Audio.VoiceGenerator>(out var voice)) voice.Babble(answer, 0.9f);
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // The effect and its cost were applied at the time the
            // response was chosen. Later orders are independently voted
            // upon and automatically invalidate this consent.
            // Итог — и в журнал: перечитать, кого и чем купили.
            Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.T("Шёпот") + ": " + outcome);
            LastResult = $"{(w != null ? w.DisplayName : "?")} ({sin}): «{offer}» — «{answer}», {(applied ? "поддался" : yielded ? "приказ изменён" : virtuous ? "не купишь" : "кошель пуст")} => {outcome}";
            Debug.Log("[ШЁПОТ] " + LastResult);

            if (focus != null) StopCoroutine(focus);
            if (slowed && Mathf.Approximately(Time.timeScale, Slow)) Time.timeScale = 1f;
            if (focus != null)
            {
                camera.StopSway();
                yield return camera.RestoreCamera();
            }
            _busy = false;
        }

        /// <summary>
        /// Чем кончился шёпот — одной строкой, словами: пошёл ли, чем заплачено,
        /// что осталось в душе. Без цифр, кроме монет: монеты игрок считает сам.
        /// </summary>
        public static string Outcome(Warrior w, SinType sin, bool yielded, bool virtuous, bool louder)
        {
            string name = w != null ? Loc.Name(w.DisplayName) : "";
            var gender = w != null ? w.Gender : default;
            if (yielded)
            {
                string done = Grammar.Pick(gender,
                    Loc.F("{0} поддался — исполнит приказ.", name),
                    Loc.F("{0} поддалась — исполнит приказ.", name));
                if (louder)
                    done += " " + Grammar.Pick(gender,
                        Loc.F("{0} в нём теперь громче.", SoulData.GetSinName(sin)),
                        Loc.F("{0} в ней теперь громче.", SoulData.GetSinName(sin)));
                if (sin == SinType.Greed) done += " " + Loc.F("Из кошеля — {0} монеты.", GreedCoins);
                return done;
            }
            if (virtuous)
                return Grammar.Pick(gender,
                    Loc.F("{0} не поддался: {1} в нём сильнее греха.", name, SoulData.GetVirtueName(sin)),
                    Loc.F("{0} не поддалась: {1} в ней сильнее греха.", name, SoulData.GetVirtueName(sin)));
            return Loc.T("Жадному нечего обещать: кошель пуст.");
        }

        /// <summary>Глаза вспыхивают цветом греха и гаснут к своему — за полторы секунды реального времени.</summary>
        private static IEnumerator Flare(Warrior w, Color colour)
        {
            var eyes = new List<(Material m, Color was)>();
            foreach (var r in w.GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)
                    if (m != null && m.name.StartsWith("Eye") && m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        eyes.Add((m, m.GetColor("_EmissionColor")));
                    }
            float t = 0f;
            while (t < 1.5f)
            {
                float k = 1f - t / 1.5f;
                foreach (var (m, was) in eyes)
                    if (m != null) m.SetColor("_EmissionColor", Color.Lerp(was, colour * 3f, k));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            foreach (var (m, was) in eyes) if (m != null) m.SetColor("_EmissionColor", was);
        }
    }
}
