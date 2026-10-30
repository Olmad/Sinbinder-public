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

        private static readonly Dictionary<Warrior, float> Until = new();
        private static readonly Dictionary<Warrior, float> Last = new();
        private static bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() { Until.Clear(); Last.Clear(); _busy = false; }

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
            if (w == null || !Until.TryGetValue(w, out var t)) return false;
            if (!w.HasCommand || Time.time > t) { Until.Remove(w); return false; }
            return true;
        }

        /// <summary>Кому сейчас можно шепнуть: свой, жив, колеблется или отказал, рядом с Греховодом.</summary>
        public static bool Can(Warrior w)
        {
            if (w == null || w.IsDead || w is SinbinderPlayer || w.Team != Team.Player || _busy) return false;
            if (!SinbinderPlayer.Exists || SinbinderPlayer.Instance.IsDead) return false;
            if (CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position) > Reach) return false;
            if (Persuaded(w)) return false;
            if (Last.TryGetValue(w, out var at) && Time.time - at < Again) return false;
            var mind = w.GetComponent<AOS.AOSWarriorWrapper>();
            if (mind == null || !w.HasCommand) return false;
            var d = mind.LastDecisionDetail;
            return d.Hesitated || d.RefusedCommand;
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Z) || Time.timeScale <= 0f) return;
            // Кому — ближнему к Греховоду из тех, кому можно.
            Warrior best = null;
            float near = float.MaxValue;
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.None))
            {
                if (!Can(w)) continue;
                float d = CampFocus.GroundDistance(SinbinderPlayer.Where, w.transform.position);
                if (d < near) { near = d; best = w; }
            }
            if (best != null) StartCoroutine(Moment(best));
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

            bool slowed = Time.timeScale > 0f;
            if (slowed) Time.timeScale = Slow;
            var camera = Dialogue.DialogueCameraController.Instance;
            Coroutine focus = null;
            if (camera != null && !camera.InDialogue)
            {
                camera.SaveCameraPosition();
                focus = StartCoroutine(camera.FocusOn(w.transform));
            }
            UI.SpeechBubbles.Say(hero, offer, 2.6f);
            if (yielded) StartCoroutine(Flare(w, SinPalette.Of(sin)));

            float t = 0f;
            bool answered = false;
            while (t < Seconds)
            {
                if (!answered && t >= 1.0f)
                {
                    answered = true;
                    UI.SpeechBubbles.Say(w, answer, 2.4f);
                    if (w.TryGetComponent<Audio.VoiceGenerator>(out var voice)) voice.Babble(answer, 0.9f);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (yielded)
            {
                if (sin == SinType.Greed) bag.SpendGold(GreedCoins);
                Until[w] = Time.time + Holds;
                // Развращение: поддался — грех громче, мягко и с потолком.
                float now = w.Soul != null ? w.Soul.Get(sin) : Ceiling;
                if (now < Ceiling) w.Soul.Change(sin, Mathf.Min(Corrupts, Ceiling - now));
            }
            Debug.Log($"[ШЁПОТ] {w.DisplayName} ({sin}): {(yielded ? "поддался" : virtuous ? "не купишь" : "кошель пуст")}");

            if (focus != null) StopCoroutine(focus);
            if (slowed && Mathf.Approximately(Time.timeScale, Slow)) Time.timeScale = 1f;
            if (focus != null)
            {
                camera.StopSway();
                yield return camera.RestoreCamera();
            }
            _busy = false;
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
