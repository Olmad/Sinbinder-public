// Assets/Scripts/Gameplay/CryptArrival.cs
// Перевод: текст через Loc
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sinbinder.Core;
namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Прибытие в склеп: сперва потеря, потом дело (docs/37-DEMO.md §0).
    ///
    /// Автор, 10 октября: «У Греховода умер самый главный воин, он чудом
    /// спасся от неминуемой смерти и такой: „Ладно, плевать, что там у нас
    /// по экспедициям?“». До этого склеп открывался заставкой и сразу
    /// панелью «Отряд ждёт платы» — без единого слова о Каргане.
    ///
    /// Теперь, когда заставка ушла: тишина, и кто-то из отряда говорит,
    /// что Каргана нет — и как его не стало. Ещё тишина. Потом беда,
    /// на которую отвечает склеп: «нас мало, а они вернутся», — и если
    /// мастерская открыта и есть кого поднять, куда идти с этой бедой.
    /// Плата спрашивается после (<see cref="UI.SalaryPanelUI"/>): одна новая
    /// вещь за раз.
    ///
    /// Карган дошёл (игрок довёл его до круга) — говорит он сам, тот же
    /// смысл без потери. «Ранен» он не говорит: здоровье между сценами
    /// не переносится, и слово соврало бы (правило пролога §2).
    ///
    /// Как не стало — решает побег, а не эта сцена: <see cref="EscapeZone.Rearguard"/>
    /// и <see cref="EscapeZone.RearguardFell"/>. Ничего случайного: кто
    /// говорит — старший по навыку командования из дошедших.
    /// </summary>
    public class CryptArrival : MonoBehaviour
    {
        /// <summary>Как Карган не дошёл до склепа — или дошёл.</summary>
        public enum Fate
        {
            /// <summary>Дошёл со всеми.</summary>
            Here,
            /// <summary>Остался прикрывать отход и пал до ухода.</summary>
            FellCovering,
            /// <summary>Остался прикрывать отход; когда уходили, ещё стоял.</summary>
            StayedCovering,
            /// <summary>Не дошёл, а как — побег не знает: пал в бою или отстал.</summary>
            Missing,
        }

        /// <summary>Тишина до первого слова, секунд: отряд стоит в склепе молча.</summary>
        private const float Hush = 2f;

        /// <summary>Тишина между потерей и делом.</summary>
        private const float Grief = 2.5f;

        private static readonly string Kargan = Loc.N("Карган Старый Ворон");

        /// <summary>
        /// Склеп ещё встречает отряд. Пока так — плата не спрашивает,
        /// а эпилог не считает (<see cref="PrologueDirector"/>).
        /// </summary>
        public static bool Running { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Running = false;

        void Awake()
        {
            // В Awake, а не в Start: плата ждёт заставку в своём Start,
            // и порядок Start Unity не обещает.
            Running = true;
        }

        private Fate _fate;

        void Start()
        {
            // Судьба Каргана — по составу: он в нём, если дошёл, а состав
            // спавнер читает тот же. Знать её надо уже сейчас: потеря
            // начинается тишиной, ещё под заставкой. Тему склепа, которую
            // музыка поставила по сцене, глушим, не дав ей встать.
            bool here = SquadRoster.TryGet(Kargan, out var k) && !k.IsAway;
            _fate = FateOf(here, EscapeZone.Rearguard == Kargan, EscapeZone.RearguardFell);
            if (_fate != Fate.Here) Audio.Music.Stop(0.5f);

            StartCoroutine(Meet());
        }

        void OnDestroy() => Running = false;

        private IEnumerator Meet()
        {
            // Сперва заставка на чёрном: потеря под полотном не прочтётся.
            // Кадр ожидания — заставка ставит паузу в своём Start.
            yield return null;
            yield return Beat.Until(Quiet, 30f, Loc.T("Заставка склепа не ушла — склеп встречает поверх неё."));

            // Отряд ставит спавнер в своём Start — ещё кадр.
            yield return null;

            // Стоит в склепе — значит дошёл, что бы ни сказал состав: сцену
            // склепа, запущенную саму по себе, спавнер заполняет каноническим
            // отрядом уже после нашего Start, и Карган иначе оплакал бы себя.
            var kargan = Living(Kargan);
            if (kargan != null) _fate = Fate.Here;

            var speaker = kargan ?? Eldest();
            string name = speaker != null ? Loc.Name(speaker.DisplayName) : null;

            if (_fate != Fate.Here)
            {
                // Потеря — в тишине: отряд стоит в склепе молча. Сигнал
                // «Whisper of the Fallen» — если ещё не звучал: пал прикрывая —
                // звучал в миг смерти, и второй раз он не событие.
                yield return new WaitForSecondsRealtime(Hush);

                if (_fate != Fate.FellCovering) Audio.Music.Play(Audio.Cue.Fallen);
                yield return Say(Loss(_fate), name);
                yield return new WaitForSecondsRealtime(Grief);
            }

            bool workshop = Crypt.CryptWorkshop.Waiting;
            yield return Say(Few(_fate, workshop), name);

            if (workshop)
                Object.FindFirstObjectByType<UI.BattleLogUI>()?.Write(Loc.T(Workshop));

            // Дальше — дело, и тема склепа возвращается. Звучит — ничего
            // не делает: та же тема не начинается заново.
            Audio.Music.Play(Audio.Track.Crypt, 4f);

            Running = false;
        }

        /// <summary>
        /// Где мастерская. Строка журнала, а не реплика: это не слова
        /// воина, а то, что игрок видит, если посмотрит налево.
        /// </summary>
        private static readonly string Workshop
            = Loc.N("Устройство, которое вселяет душу в тело, стоит у левой стены. Души — в суме.");

        private static bool Quiet()
            => !UI.StartPanel.Waiting && !UI.PrologueTitleUI.Showing && !Core.GamePauseController.Stopped;

        /// <summary>
        /// Как Карган не дошёл. Дошёл — <see cref="Fate.Here"/>; остался
        /// прикрывать — пал или стоял, как застал уход; иначе побег
        /// не знает как. Отдельно от сцены — самопроверке.
        /// </summary>
        public static Fate FateOf(bool here, bool covered, bool fell)
        {
            if (here) return Fate.Here;
            if (covered) return fell ? Fate.FellCovering : Fate.StayedCovering;
            return Fate.Missing;
        }

        /// <summary>
        /// Слова о потере — строками вида «{0}: «…».», где {0} — говорящий.
        /// Пусто — потери нет.
        /// </summary>
        public static IReadOnlyList<string> Loss(Fate fate)
        {
            switch (fate)
            {
                case Fate.FellCovering:   return new[] { FellLine };
                case Fate.StayedCovering: return new[] { StayedLine };
                case Fate.Missing:        return new[] { MissingLine };
                default:                  return new string[0];
            }
        }

        /// <summary>
        /// Беда, на которую отвечает склеп: людей мало, а охота не кончилась.
        /// Мастерская ждёт — следом, куда с этой бедой идти; нет — беда
        /// остаётся сказанной, и ответ на неё в полной игре.
        /// </summary>
        public static IReadOnlyList<string> Few(Fate fate, bool workshop)
        {
            string few = fate == Fate.Here ? FewHereLine : FewLine;
            return workshop ? new[] { few, SoulsLine } : new[] { few };
        }

        // Строки — целиком, с местом под имя: переводчик видит фразу, а не обрывок.
        private static readonly string FellLine = Loc.N(
            "{0}: «Карган пал у лагеря, владыка. Он держал их на себе, пока мы уходили».");
        private static readonly string StayedLine = Loc.N(
            "{0}: «Карган остался у лагеря, чтобы мы успели уйти. Один против них он не выстоит».");
        private static readonly string MissingLine = Loc.N(
            "{0}: «Карган не дошёл с нами, владыка. Он был при вас дольше всех нас».");
        private static readonly string FewLine = Loc.N(
            "{0}: «Нас осталось мало, владыка, а Охотники вернутся».");
        private static readonly string FewHereLine = Loc.N(
            "{0}: «Мы ушли, владыка, но нас осталось мало. Охотники вернутся, и в другой раз уйти не дадут».");
        private static readonly string SoulsLine = Loc.N(
            "{0}: «Души, что вы собрали в лагере, при вас. Вложите их в пустые тела — и они будут драться за нас».");

        /// <summary>
        /// Сказать голосом <paramref name="speaker"/> и дождаться, пока кадр
        /// освободится. Говорить некому — до склепа не дошёл никто, кроме
        /// Греховода, — и слов нет: «нас мало» без «нас» не говорят.
        /// </summary>
        private static IEnumerator Say(IReadOnlyList<string> lines, string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) yield break;
            foreach (var line in lines) Herald.Line(Loc.F(line, speaker));

            while (Herald.Busy) yield return null;
        }

        /// <summary>Живой свой воин с этим именем, не Греховод.</summary>
        private static Warrior Living(string name)
        {
            foreach (var w in Object.FindObjectsByType<Warrior>(FindObjectsSortMode.InstanceID))
                if (w != null && !w.IsDead && w.Team == Team.Player
                    && !(w is SinbinderPlayer) && w.DisplayName == name)
                    return w;
            return null;
        }

        /// <summary>
        /// Кто говорит вместо Каргана: старший по навыку командования
        /// из дошедших, при равных — первый по составу.
        /// </summary>
        private static Warrior Eldest()
        {
            Warrior best = null;
            float lead = -1f;

            foreach (var m in SquadRoster.Members)
            {
                if (m.IsAway || m.Leadership <= lead) continue;
                var w = Living(m.Name);
                if (w == null) continue;

                best = w;
                lead = m.Leadership;
            }

            return best;
        }
    }
}
