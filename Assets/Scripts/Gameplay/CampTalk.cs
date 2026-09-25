// Assets/Scripts/Gameplay/CampTalk.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Разговоры у костра — шаг второй жизни в лагере (docs/32-CAMP.md §6).
    /// Места дают лагерю тело, разговоры — голос: раз в минуту одна пара
    /// обменивается двумя строками над головами (<see cref="Dialogue.CampLines"/>).
    ///
    /// <b>Кто.</b> Двое, что стоят на одном месте или ближе трёх с половиной
    /// метров; из таких пар — та, что дольше всех молчала; при равенстве —
    /// по именам. Первым говорит тот, кто молчал дольше. Без жребия.
    ///
    /// <b>Когда.</b> Только пока лагерь живёт (<see cref="CampLife.Idle"/>):
    /// не во время сцен пролога и не у того, кому отдан приказ. Журнал
    /// не заваливается: лагерь — фон, а не лента новостей.
    ///
    /// За выключателем «лагерь». Ставит себя сам.
    /// </summary>
    public class CampTalk : MonoBehaviour
    {
        /// <summary>Раз во сколько секунд игры говорит одна пара.</summary>
        public const float Every = 60f;

        /// <summary>Ближе этого двое — уже рядом, даже на разных местах.</summary>
        public const float Near = 3.5f;

        private const float FirstAfter = 20f;
        private const float AnswerAfter = 2.5f;

        private static CampTalk _instance;
        private readonly Dictionary<Warrior, float> _spoke = new();
        private float _next = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("Разговоры лагеря");
            _instance = go.AddComponent<CampTalk>();
            DontDestroyOnLoad(go);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (!CampLife.Enabled) { _next = -1f; return; }
            if (_next < 0f) { _next = Time.time + FirstAfter; return; }
            if (Time.time < _next) return;
            _next = Time.time + Every;

            if (!Pick(out var first, out var second)) return;

            var (line, answer) = Dialogue.CampLines.Exchange(first, second);
            UI.SpeechBubbles.Say(first, line);
            StartCoroutine(AnswerLater(second, answer));

            _spoke[first] = Time.time;
            _spoke[second] = Time.time;
        }

        private static IEnumerator AnswerLater(Warrior who, string line)
        {
            yield return new WaitForSeconds(AnswerAfter);
            if (who != null && !who.IsDead) UI.SpeechBubbles.Say(who, line);
        }

        /// <summary>Пара, что дольше всех молчала; первым — кто молчал дольше.</summary>
        private bool Pick(out Warrior first, out Warrior second)
        {
            first = second = null;

            var idle = new List<Warrior>();
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (CampLife.Idle(w)) idle.Add(w);
            idle.Sort((x, y) => string.CompareOrdinal(x.DisplayName, y.DisplayName));

            // Забытые — те, кого в сцене больше нет.
            var gone = new List<Warrior>();
            foreach (var k in _spoke.Keys) if (k == null) gone.Add(k);
            foreach (var k in gone) _spoke.Remove(k);

            float best = float.MaxValue;
            for (int i = 0; i < idle.Count; i++)
                for (int j = i + 1; j < idle.Count; j++)
                {
                    var a = idle[i];
                    var b = idle[j];
                    if (!Together(a, b)) continue;

                    float heard = Mathf.Max(Last(a), Last(b));
                    if (heard >= best) continue;   // при равенстве остаётся первая по именам
                    best = heard;
                    first = Last(a) <= Last(b) ? a : b;
                    second = first == a ? b : a;
                }

            return first != null;
        }

        private float Last(Warrior w) => _spoke.TryGetValue(w, out float t) ? t : float.MinValue;

        private static bool Together(Warrior a, Warrior b)
        {
            if (CampLife.SpotOf(a, out var sa) && CampLife.SpotOf(b, out var sb) && sa == sb) return true;
            return CampFocus.GroundDistance(a.transform.position, b.transform.position) <= Near;
        }
    }
}
