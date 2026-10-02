// Assets/Scripts/AOS Engine/RefusalPresenter.cs
// Перевод: текст через Loc
using System.Collections;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Как выглядит момент отказа.
    ///
    /// Прежние три бесплатных приёма: значок укрупняется, воин доворачивает
    /// корпус к тому, что выбрал вместо приказа, и звучит бип его греха.
    ///
    /// С 2 октября — <b>отказ как событие</b> (41-SHOWCASE п. 4,
    /// 42-INTERFACE §1 п. 2): главное, что продаёт игру, было строкой журнала.
    /// Теперь в миг отказа ещё три вещи:
    ///
    /// <list type="bullet">
    /// <item><b>мир на миг замедляется</b> — полсекунды настоящего времени,
    ///       не чаще раза в несколько секунд на весь бой: зритель успевает
    ///       перевести взгляд, а бой не превращается в заикание;</item>
    /// <item><b>реплика облачком</b> от первого лица — коротко, по той же
    ///       причине, что в журнале и голосах: «Сначала заплатите.»,
    ///       «Их слишком много!»;</item>
    /// <item><b>подсветка</b> — вспышка света цвета его греха у ног,
    ///       полторы секунды, и гаснет: вспыхнуло и тлеет, как и слово
    ///       над головой (<see cref="UI.OverheadWord"/>).</item>
    /// </list>
    ///
    /// Только свой отряд и только видимый: в тумане отказа не видно.
    /// Компонент необязательный: без него отказ всё равно происходит,
    /// просто тише.
    /// </summary>
    [RequireComponent(typeof(Warrior))]
    public class RefusalPresenter : MonoBehaviour
    {
        [SerializeField] private float _iconScale = 1.6f;
        [SerializeField] private float _turnSpeed = 12f;
        [SerializeField] private bool _speak = true;

        [Header("Отказ как событие")]
        [Tooltip("Во сколько раз замедлить мир в миг отказа.")]
        [SerializeField] private float _beatScale = 0.3f;

        [Tooltip("Сколько держать замедление, секунды настоящего времени.")]
        [SerializeField] private float _beatSeconds = 0.55f;

        [Tooltip("Не чаще, чем раз в столько секунд на весь бой: замедление на "
               + "каждом отказе превращает бой в заикание.")]
        [SerializeField] private float _beatCooldown = 6f;

        [Tooltip("Сколько горит подсветка, секунды настоящего времени.")]
        [SerializeField] private float _glowSeconds = 1.6f;

        private Warrior _warrior;
        private Coroutine _running;

        /// <summary>Когда можно замедлить мир снова — общее на всех воинов.</summary>
        private static float _nextBeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => _nextBeat = 0f;

        void Awake() => _warrior = GetComponent<Warrior>();

        public void Play(Decision decision, DecisionContext context)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Routine(decision, context));

            // Событием отказ делается только у своего и только на виду.
            if (_warrior == null || _warrior.Team != Team.Player || FogOfWar.Hides(_warrior)) return;

            StartCoroutine(Beat());
            StartCoroutine(Glow());

            string retort = Retort(decision, context);
            if (!string.IsNullOrEmpty(retort)) UI.SpeechBubbles.Say(_warrior, retort, 3.5f);
        }

        private IEnumerator Routine(Decision decision, DecisionContext context)
        {
            if (_speak)
            {
                var voice = GetComponent<Audio.VoiceGenerator>();
                if (voice != null) voice.Speak();
            }

            var icon = GetComponentInChildren<UI.DecisionIconUI>();
            Transform iconT = icon != null ? icon.transform : null;
            Vector3 original = iconT != null ? iconT.localScale : Vector3.one;
            if (iconT != null) iconT.localScale = original * _iconScale;

            Vector3 look = LookTarget(decision, context);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;

                if (look != Vector3.zero)
                {
                    Vector3 dir = look - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(dir), Time.deltaTime * _turnSpeed);
                }

                yield return null;
            }

            if (iconT != null) iconT.localScale = original;
            _running = null;
        }

        /// <summary>
        /// Мир на миг медленнее. Только если время идёт как обычно: пауза,
        /// урок, разговор или ускорение прогона — чужие, их не трогаем.
        /// Возвращаем, только если за это время никто не поменял скорость сам.
        /// </summary>
        private IEnumerator Beat()
        {
            if (Time.unscaledTime < _nextBeat) yield break;
            if (GamePauseController.Stopped || !Mathf.Approximately(Time.timeScale, 1f)) yield break;

            _nextBeat = Time.unscaledTime + _beatCooldown;
            Time.timeScale = _beatScale;

            yield return new WaitForSecondsRealtime(_beatSeconds);

            if (Mathf.Approximately(Time.timeScale, _beatScale)) Time.timeScale = 1f;
        }

        /// <summary>
        /// Вспышка цвета греха у ног: вспыхнула — погасла. Свет без теней,
        /// как огонь греха у глаз: на поле их девять, тени стоили бы дороже.
        /// </summary>
        private IEnumerator Glow()
        {
            var go = new GameObject("Подсветка отказа");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.range = 3.5f;
            light.color = _warrior.Soul != null ? SinPalette.Of(_warrior.Soul.Sin) : new Color(0.9f, 0.86f, 0.78f);
            light.intensity = 0f;

            float t = 0f;
            while (t < _glowSeconds && go != null)
            {
                t += Time.unscaledDeltaTime;

                // Быстро вспыхнуть, медленно погаснуть.
                float k = t / _glowSeconds;
                light.intensity = 3.2f * (k < 0.15f ? k / 0.15f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.15f) / 0.85f));

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>
        /// Реплика отказа, от первого лица. Причина та же, что в журнале
        /// и в голосах (одно решение — одни слова по смыслу): взвешено
        /// «от противного» — по решающей причине; нет — по громкому голосу.
        /// Колебание — без реплики: над ним и так «Колеблется…».
        /// </summary>
        private string Retort(Decision d, DecisionContext c)
        {
            if (d.Hesitated) return null;

            var gender = _warrior != null ? _warrior.Gender : Gender.Male;

            switch (d.Weighed ? d.Decisive : Counterfactual.Factor.None)
            {
                case Counterfactual.Factor.Distance:     return Loc.T("Не слышно отсюда, владыка!");
                case Counterfactual.Factor.Debt:         return Loc.T("Сначала заплатите.");
                case Counterfactual.Factor.Pocket:       return Loc.T("Мне есть что терять.");
                case Counterfactual.Factor.Temptation:   return Loc.T("Это теперь моё.");
                case Counterfactual.Factor.Loot:         return Loc.T("Добро само себя не соберёт.");
                case Counterfactual.Factor.Patrol:       return Loc.T("Опять ходить взад-вперёд?");
                case Counterfactual.Factor.AllyInDanger: return Loc.T("Своих не брошу!");
                case Counterfactual.Factor.Wounds:       return Grammar.Pick(gender, Loc.T("Я ранен!"), Loc.T("Я ранена!"));
                case Counterfactual.Factor.Surrounded:   return Loc.T("Их слишком много!");
                case Counterfactual.Factor.Fatigue:      return Loc.T("Сил нет…");
            }

            // Не взвешено: долг и даль игрок исправит сам — их и называем.
            if (c != null && c.UnpaidMissions > 0 && d.TopModule == "Greed") return Loc.T("Сначала заплатите.");

            string voice = d.Weighed && !string.IsNullOrEmpty(d.DecisiveVoice) ? d.DecisiveVoice : d.TopModule;
            bool virtue = Virtuous(voice);

            switch (voice)
            {
                case "Greed":    return virtue ? Loc.T("Кому-то нужнее, чем мне.") : Loc.T("Сначала моя доля.");
                case "Pride":    return virtue ? Loc.T("Простите, владыка, не могу.") : Loc.T("Я не пёс, чтобы бегать по свисту.");
                case "Wrath":    return Loc.T("Сам решу, кого бить!");
                case "Patience": return Loc.T("Не время.");
                case "Envy":     return virtue ? Loc.T("Пусть другой сходит.") : Loc.T("А почему опять я?");
                case "Lust":     return Loc.T("Позже, владыка.");
                case "Gluttony": return Loc.T("Не на пустой желудок.");
                case "Sloth":    return virtue ? Loc.T("У меня тут дело.") : Loc.T("Потом…");
                case "Fear":     return Loc.T("Не пойду туда!");
                case "Morality": return Loc.T("Этого я делать не стану.");
                case "Memory":   return Loc.T("Помню, чем это кончилось.");
                case "Virtue":   return Loc.T("Совесть не велит.");
                default:         return Loc.T("Нет.");
            }
        }

        /// <summary>Голосует ли грех своей добродетельной половиной (шкала ниже нуля).</summary>
        private bool Virtuous(string module)
        {
            var soul = _warrior != null ? _warrior.Soul : null;
            if (soul == null) return false;

            switch (module)
            {
                case "Greed": return soul.Get(SinType.Greed) < 0f;
                case "Pride": return soul.Get(SinType.Pride) < 0f;
                case "Envy":  return soul.Get(SinType.Envy) < 0f;
                case "Sloth": return soul.Get(SinType.Sloth) < 0f;
                default:      return false;
            }
        }

        /// <summary>Куда смотреть: на то, что воин выбрал вместо приказа.</summary>
        private Vector3 LookTarget(Decision decision, DecisionContext context)
        {
            switch (decision.Action)
            {
                case ActionType.SaveAlly:
                    return context?.TargetWarrior != null
                        ? context.TargetWarrior.transform.position : Vector3.zero;

                case ActionType.Flee:
                    // Отвернуться от приказа.
                    return _warrior.Command.IsSet
                        ? transform.position - (_warrior.Command.Point - transform.position)
                        : Vector3.zero;

                default:
                    return Vector3.zero;
            }
        }
    }
}
