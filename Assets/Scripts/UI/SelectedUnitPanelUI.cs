// Assets/Scripts/UI/SelectedUnitPanelUI.cs
// Перевод: текст через Loc
using UnityEngine;
using UnityEngine.UI;
using Sinbinder.Core;
using Sinbinder.Gameplay;
using Sinbinder.AOS;

namespace Sinbinder.UI
{
    /// <summary>
    /// Нижняя панель выбранного — две левые зоны макета лагеря
    /// (<c>docs/42-INTERFACE.md</c> §3): душа и «что тянет его сейчас».
    ///
    /// <b>Без выбора панели нет</b> (§1, п. 7: «без интерфейса по умолчанию»).
    /// До 1 октября никого не выделено — показывался Греховод; теперь
    /// пустой выбор — пустой низ экрана, а Греховода показывают, только
    /// когда его выбрали.
    ///
    /// <b>Душа.</b> Огонь цвета греха вместо черепа оболочки: внутри —
    /// громкий спектр, снаружи — второй (у всех скелетов один череп,
    /// а душа у каждого своя). Под именем — ремесло и тело, грехи словами,
    /// тело словами.
    ///
    /// <b>Голоса.</b> Громкий голос крупно, со словом громкости и причиной —
    /// теми же словами, что журнал (<see cref="PhraseGenerator.Reason"/>:
    /// одно решение — одни слова). Ниже — «Ваш приказ» одной строкой:
    /// приказ озвучивает верность (<c>LoyaltyModule</c>), две строки были бы
    /// второй правдой. Почему приказ тише — названо так, чтобы было ясно,
    /// чем помочь: «вы далеко: подойдите ближе», «не верит вам: заплатите».
    ///
    /// <b>Чисел нет</b> (00-GDD.md §7, 42-INTERFACE §2): громкость — словом
    /// и размером строки, тело — словом.
    /// </summary>
    public class SelectedUnitPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;

        [Header("Душа")]
        [SerializeField] private Image _flameOuter;
        [SerializeField] private Image _flameInner;
        [SerializeField] private Text _nameLine;
        [SerializeField] private Text _craftLine;
        [SerializeField] private Text _sinLine;
        [SerializeField] private Text _bodyLine;

        [Header("Голоса")]
        [SerializeField] private Text _voicesTitle;
        [SerializeField] private Image _voiceDot;
        [SerializeField] private Text _voiceLine;
        [SerializeField] private Text _reasonLine;
        [SerializeField] private Image _orderPlate;
        [SerializeField] private Text _orderLine;
        [SerializeField] private Text _orderWhy;

        /// <summary>
        /// Как часто перечитывать. Решение живёт заметно дольше кадра,
        /// а строка, меняющаяся шестьдесят раз в секунду, — мигание.
        /// </summary>
        [SerializeField] private float _refreshEvery = 0.25f;

        // Цвета макета: кость, приглушённая кость, тлеющий.
        private static readonly Color Bone = new Color(0.90f, 0.86f, 0.78f);
        private static readonly Color Muted = new Color(0.70f, 0.65f, 0.55f);
        private static readonly Color Faint = new Color(0.56f, 0.51f, 0.44f);

        private float _next;

        private static SelectedUnitPanelUI _instance;

        /// <summary>
        /// Полоса показывает воина, а не Греховода. Спрашивает сума: её место —
        /// вид Греховода, а при воине на её месте его голоса.
        /// </summary>
        public static bool ShowsWarrior { get; private set; }

        /// <summary>
        /// Лежит ли точка экрана на полосе. Спрашивает выделение, как
        /// у панели приказов: щелчок по полосе — не щелчок по земле под ней.
        /// Без этого он снимал выделение, и полоса пропадала из-под мыши.
        /// </summary>
        public static bool Covers(Vector2 screen)
        {
            if (_instance == null || _instance._panel == null || !_instance._panel.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)_instance._panel.transform, screen, null);
        }

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance == this) { _instance = null; ShowsWarrior = false; }
        }

        void Start()
        {
            if (_panel == null || _nameLine == null || _voiceLine == null || _orderLine == null)
            {
                Debug.LogWarning("[ПАНЕЛЬ] Нижняя панель не связана в сцене: "
                               + "пересоберите сцены демо.");
                enabled = false;
                return;
            }

            Refresh();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + _refreshEvery;

            Refresh();
        }

        private void Refresh()
        {
            var who = Current();
            ShowsWarrior = who != null && !(who is SinbinderPlayer);

            if (who == null)
            {
                _panel.SetActive(false);
                return;
            }

            _panel.SetActive(true);

            if (who is SinbinderPlayer) ShowSinbinder(who);
            else ShowWarrior(who);
        }

        /// <summary>
        /// Кого показывать: первого выделенного воина; Греховода — только
        /// если выделен он один. Рамка берёт и его (26 сентября), и панель
        /// с героем вместо воина врала бы, о ком речь.
        /// </summary>
        private static Warrior Current()
        {
            var manager = SelectionManager.Instance;
            if (manager == null) return null;

            Warrior hero = null;
            var selected = manager.GetSelectedUnits();
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] == null) continue;

                var warrior = selected[i].GetComponentInParent<Warrior>();
                if (warrior == null) continue;
                if (warrior is SinbinderPlayer) { hero = warrior; continue; }
                return warrior;
            }
            return hero;
        }

        // ---------- воин ----------

        private void ShowWarrior(Warrior who)
        {
            var soul = who.Soul;

            // Имя без ремесла: ремесло стоит строкой ниже, «лучник, скелет».
            // Заслуженный титул — тот же ShownName, он ремесла не несёт.
            bool titled = soul != null && !string.IsNullOrEmpty(who.Reputation?.CurrentName);
            _nameLine.text = Loc.Name(titled ? who.ShownName : who.DisplayName);
            Set(_craftLine, CraftLine(who), Muted);
            Set(_sinLine, soul != null ? SinsLine(soul) : Loc.T("Душа неизвестна"), Bone);
            Set(_bodyLine, BodyLine(who), Muted);
            Flame(soul);

            Set(_voicesTitle, Grammar.For(who.Gender, Loc.T("Что тянет его сейчас")), Bone);

            var wrapper = who.GetComponent<AOSWarriorWrapper>();
            if (wrapper == null || wrapper.LastContext == null)
            {
                Voice(null, Loc.T("Пока молчит"), "", Faint, 22);
                Order(Loc.T("Приказа нет"), "", false);
                return;
            }

            var decision = wrapper.LastDecisionDetail;
            var context = wrapper.LastContext;
            bool ordered = who.Command.IsSet;

            // Исполняет приказ — громче всех ваш голос, и он один на панели:
            // строки «Верность кричит» и «Ваш приказ громче» — одна правда дважды.
            if (ordered && !decision.Hesitated && decision.Action == ActionType.ObeyCommand)
            {
                Voice(null, "", "", Faint, 22);
                Order(Loc.T("Ваш приказ — громче всех"),
                      Loc.T("Сейчас: ") + PhraseGenerator.Doing(decision.Action), true);
                return;
            }

            string module = decision.TopModule;
            var sin = SinOf(module);

            Voice(sin.HasValue ? SinPalette.Of(sin.Value) : (Color?)null,
                  Loc.F("{0} {1}", VoiceName(module, soul), Loudness(decision)),
                  PhraseGenerator.Reason(who, context, decision),
                  Bone, decision.Hesitated ? 30 : 34);

            if (!ordered)
            {
                Order(Loc.T("Приказа нет"), Grammar.For(who.Gender, Loc.T("Он решает сам")), false);
                return;
            }

            string why = Grammar.For(who.Gender, Quieter(decision, context));
            if (decision.Hesitated)
                Order(Loc.T("Ваш приказ — почти так же громко"), why, true);
            else
                Order(Loc.T("Ваш приказ — тише"), why, true);
        }

        /// <summary>Ремесло и тело одной строкой, строчными: «крестьянин, зомби».</summary>
        private static string CraftLine(Warrior who)
        {
            string body = Crypt.CryptHands.ShellName(who.Shell);
            string craft = who.Soul != null ? Trades.Name(who.Soul.Trade) : "";
            string line = string.IsNullOrEmpty(craft) ? body : Loc.F("{0}, {1}", Loc.T(craft), body);
            return line.ToLowerInvariant();
        }

        /// <summary>
        /// Два самых громких спектра словами: «Жадность жжёт, Уныние тлеет».
        /// Ниже нуля — добродетель, своим именем: щедрость — это жадность
        /// со знаком минус, а не отсутствие жадности.
        /// </summary>
        private static string SinsLine(SoulData soul)
        {
            SoulJarGlow.Read(soul, out var loudest, out var next, out _, out _);

            string first = Spectrum(soul, loudest);
            if (next == loudest || Mathf.Abs(soul.Get(next)) < 10f) return first;
            return Loc.F("{0}, {1}", first, Spectrum(soul, next));
        }

        private static string Spectrum(SoulData soul, SinType sin)
        {
            float v = soul.Get(sin);
            string name = v < 0f ? SoulData.GetVirtueName(sin) : SoulData.GetSinName(sin);
            float a = Mathf.Abs(v);

            if (a >= 60f) return Loc.F("{0} жжёт", name);
            if (a >= 25f) return Loc.F("{0} тлеет", name);
            return Loc.F("{0} дремлет", name);
        }

        /// <summary>Тело словом. Цифры здоровья нет и здесь.</summary>
        private static string BodyLine(Warrior who)
        {
            float max = who.MaxHP;
            float share = max > 0f ? who.HP / max : 1f;

            if (share >= 0.95f) return Loc.T("Тело: цело");
            if (share >= 0.6f) return Loc.T("Тело: задето");
            if (share >= 0.3f) return Loc.T("Тело: ранено");
            return Loc.T("Тело: едва держится");
        }

        /// <summary>Огонь души: внутри громкий спектр, снаружи второй.</summary>
        private void Flame(SoulData soul)
        {
            if (_flameOuter == null || _flameInner == null) return;

            if (soul == null)
            {
                _flameOuter.color = Faint;
                _flameInner.color = new Color(0f, 0f, 0f, 0f);
                return;
            }

            SoulJarGlow.Read(soul, out var loudest, out var next, out _, out bool virtue);

            // В покое огонь тлеет (§1, п. 1–2): яркость — расход, и панель,
            // которая видна весь бой, гореть в полную силу не должна.
            _flameInner.color = Dim(SoulJarGlow.Tone(loudest, virtue));
            _flameOuter.color = Dim(SinPalette.Of(next)) * new Color(1f, 1f, 1f, 0.85f);
        }

        private static Color Dim(Color c) => new Color(c.r * 0.72f, c.g * 0.72f, c.b * 0.72f, 1f);

        // ---------- Греховод ----------

        /// <summary>
        /// Греховод: пустая капля без цвета греха — своей души у него в этом
        /// смысле нет, он не голосует. Сума и кошель вместо голосов — следующий шаг.
        /// </summary>
        private void ShowSinbinder(Warrior who)
        {
            _nameLine.text = Loc.T("Греховод");
            Set(_craftLine, Loc.T("это вы: слушается всегда"), Muted);
            Set(_sinLine, "", Bone);
            Set(_bodyLine, BodyLine(who), Muted);

            if (_flameOuter != null) _flameOuter.color = Faint;
            if (_flameInner != null) _flameInner.color = new Color(0.10f, 0.08f, 0.07f, 1f);

            Set(_voicesTitle, Loc.T("Голосов у него нет"), Faint);

            var walk = who.GetComponent<PlayerWalk>();
            var legs = who.GetComponent<UnitMover>();
            bool going = (walk != null && walk.Walking) || (legs != null && legs.IsMoving);

            Voice(null, going ? Loc.T("Идёт") : Loc.T("Стоит"), "", Muted, 26);
            Order(Loc.T("Приказ — всему отряду"), Loc.T("Отряд слышит вас тем хуже, чем вы дальше"), false);
        }

        // ---------- голоса ----------

        private void Voice(Color? dot, string line, string reason, Color color, int size)
        {
            if (_voiceDot != null)
            {
                _voiceDot.enabled = dot.HasValue;
                if (dot.HasValue) _voiceDot.color = Dim(dot.Value);
            }

            _voiceLine.text = line;
            _voiceLine.color = color;
            _voiceLine.fontSize = size;
            Set(_reasonLine, reason, Muted);
        }

        private void Order(string line, string why, bool plate)
        {
            _orderLine.text = line;
            _orderLine.color = plate ? Bone : Faint;
            Set(_orderWhy, why, plate ? Bone : Faint);
            if (_orderPlate != null) _orderPlate.enabled = plate;
        }

        private static void Set(Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value ?? "";
            text.color = color;
        }

        /// <summary>
        /// Громкость словом. Уверенность — разрыв в долях от громкости
        /// победителя (<see cref="Decision.Confidence"/>), а не голые очки:
        /// в тихом лагере и в гуще боя те же очки значат разное.
        /// </summary>
        private static string Loudness(Decision d)
        {
            if (d.Hesitated) return Loc.T("звучит почти вровень с другим");
            if (d.Confidence >= 0.5f) return Loc.T("кричит");
            if (d.Confidence >= 0.2f) return Loc.T("говорит громко");
            return Loc.T("говорит чуть громче прочих");
        }

        /// <summary>
        /// Почему приказ тише — так, чтобы было видно, чем помочь.
        /// Взвешено «от противного» (<see cref="Counterfactual"/>) — решающая
        /// причина; нет — по положению: даль и долг игрок исправит сам.
        /// </summary>
        private static string Quieter(Decision d, DecisionContext c)
        {
            var f = d.Weighed ? d.Decisive : Counterfactual.Factor.None;

            if (f == Counterfactual.Factor.None)
            {
                if (c.CommandVolume < 0.75f) f = Counterfactual.Factor.Distance;
                else if (c.UnpaidMissions > 0) f = Counterfactual.Factor.Debt;
            }

            switch (f)
            {
                case Counterfactual.Factor.Distance:     return Loc.T("вы далеко: подойдите ближе");
                case Counterfactual.Factor.Debt:         return Loc.T("не верит вам: отдайте долг");
                case Counterfactual.Factor.Pocket:       return Loc.T("ему есть что терять");
                case Counterfactual.Factor.Temptation:   return Loc.T("вещь в руках тянет его в другую сторону");
                case Counterfactual.Factor.Loot:         return Loc.T("добыча лежит слишком близко");
                case Counterfactual.Factor.Patrol:       return Loc.T("приказ ему скучен");
                case Counterfactual.Factor.AllyInDanger: return Loc.T("свой в беде");
                case Counterfactual.Factor.Wounds:       return Loc.T("он ранен");
                case Counterfactual.Factor.Surrounded:   return Loc.T("его обступили");
                case Counterfactual.Factor.Fatigue:      return Loc.T("силы на исходе");
                default:                                 return Loc.T("тише, чем его душа");
            }
        }

        /// <summary>Шкала, которой голосует модуль греха. Не грех — пусто.</summary>
        private static SinType? SinOf(string module)
        {
            switch (module)
            {
                case "Greed":    return SinType.Greed;
                case "Pride":    return SinType.Pride;
                case "Wrath":    return SinType.Wrath;
                case "Patience": return SinType.Wrath;
                case "Envy":     return SinType.Envy;
                case "Lust":     return SinType.Lust;
                case "Gluttony": return SinType.Gluttony;
                case "Sloth":    return SinType.Sloth;
                default:         return null;
            }
        }

        /// <summary>
        /// Имя голоса. У греха — имя той половины шкалы, что голосовала:
        /// у щедрого голосует не «Жадность», а щедрость.
        /// </summary>
        private static string VoiceName(string module, SoulData soul)
        {
            var sin = SinOf(module);
            if (sin.HasValue)
            {
                bool virtue = module == "Patience" || (soul != null && soul.Get(sin.Value) < 0f);
                return virtue ? SoulData.GetVirtueName(sin.Value) : SoulData.GetSinName(sin.Value);
            }

            switch (module)
            {
                case "Fear":     return Loc.T("Страх");
                case "Memory":   return Loc.T("Память");
                case "Loyalty":  return Loc.T("Верность");
                case "Morality": return Loc.T("Совесть");
                case "Virtue":   return Loc.T("Добродетель");
                default:         return Loc.T("Его душа");
            }
        }
    }
}
