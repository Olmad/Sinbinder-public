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

        [Header("Руки Греховода")]
        [SerializeField] private Text _handsTitle;
        [SerializeField] private Image _talkPlate;
        [SerializeField] private Text _talkText;
        [SerializeField] private Image _harvestPlate;
        [SerializeField] private Text _harvestText;
        [SerializeField] private Text _handsHint;

        [Header("Голоса")]
        [SerializeField] private Text _voicesTitle;
        [SerializeField] private Image _voiceDot;
        [SerializeField] private Text _voiceLine;
        [SerializeField] private Text _reasonLine;
        [SerializeField] private Image _orderPlate;
        [SerializeField] private Text _orderLine;
        [SerializeField] private Text _orderWhy;
        [SerializeField] private Text _groupLine;

        [Header("Греховод: вместо голосов")]
        [SerializeField] private Text _satchelText;
        [SerializeField] private Text _purseText;

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
        /// Полоса видна — кто-то выбран. Спрашивает полоса сумы: при выборе
        /// на её месте голоса или зона приказов, а сама сума — в виде
        /// Греховода на полосе. Считается на месте, а не берётся из прошлого
        /// обновления полосы: у сумы и полосы свои часы, и кадр между ними
        /// показывал обе сразу (прогон -All 1 октября, кадр прогноза).
        /// </summary>
        public static bool ShowsAnyone => Current() != null;

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
            if (_instance == this) _instance = null;
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

            Dress();
            Refresh();
        }

        /// <summary>
        /// Облик макета (<see cref="UiStyle"/>): капля огня души и овалы рук.
        /// Спрайты рисуются кодом и в сцену не сохраняются — ставятся здесь.
        /// </summary>
        private void Dress()
        {
            foreach (var flame in new[] { _flameOuter, _flameInner })
            {
                if (flame == null) continue;
                flame.sprite = UiStyle.Drop;
                flame.preserveAspect = true;
            }

            foreach (var plate in new[] { _talkPlate, _harvestPlate })
            {
                if (plate == null) continue;
                plate.sprite = UiStyle.Pill;
                plate.type = Image.Type.Sliced;
                plate.pixelsPerUnitMultiplier = 1.4f;
            }

            if (_orderPlate != null)
            {
                _orderPlate.sprite = UiStyle.Pill;
                _orderPlate.type = Image.Type.Sliced;
                _orderPlate.pixelsPerUnitMultiplier = 3.5f;   // почти прямой угол: плашка, а не таблетка
            }
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

            if (who == null)
            {
                _panel.SetActive(false);
                return;
            }

            _panel.SetActive(true);

            bool hero = who is SinbinderPlayer;
            Shown(!hero);

            if (hero) ShowSinbinder(who);
            else { ShowWarrior(who); Group(who); }

            Hands(who);
        }

        /// <summary>Голоса воина или сума Греховода — одно из двух на том же месте.</summary>
        private void Shown(bool voices)
        {
            foreach (var g in new Graphic[] { _voiceDot, _voiceLine, _reasonLine, _orderPlate, _orderLine, _orderWhy, _groupLine })
                if (g != null) g.gameObject.SetActive(voices);
            foreach (var g in new Graphic[] { _satchelText, _purseText })
                if (g != null) g.gameObject.SetActive(!voices);
        }

        // ---------- группа ----------

        /// <summary>
        /// Выбрано несколько (42-INTERFACE §3, «Группа»): в портрете тот, кто
        /// громче всех не согласен, — его покажет <see cref="Current"/>; здесь —
        /// заголовок «Выбрано пятеро · что тянет его» и строка про остальных:
        /// «Лиска медлит · ещё трое — в строю». Числа — словами.
        /// </summary>
        private void Group(Warrior shown)
        {
            var all = Picked();
            if (_groupLine != null) _groupLine.text = "";
            if (all.Count < 2) return;

            Set(_voicesTitle, Grammar.For(shown.Gender,
                Loc.F("Выбрано {0} · что тянет его", Many(all.Count))), Bone);
            if (_craftLine != null && Disagrees(shown))
                _craftLine.text = Grammar.For(shown.Gender, Loc.T("громче всех не согласен"));

            var odd = new System.Collections.Generic.List<string>();
            int calm = 0;
            foreach (var w in all)
            {
                if (w == shown) continue;
                var d = w.GetComponent<AOSWarriorWrapper>();
                if (d != null && d.LastContext != null && Disagrees(w))
                    odd.Add(Loc.F("{0} {1}", Loc.Name(w.DisplayName),
                        d.LastDecisionDetail.Hesitated ? Grammar.For(w.Gender, Loc.T("медлит"))
                                                       : PhraseGenerator.Doing(d.LastDecisionDetail.Action).ToLowerInvariant()));
                else calm++;
            }

            if (calm > 0) odd.Add(Loc.F("ещё {0} — в строю", Many(calm)));
            if (_groupLine != null)
            {
                _groupLine.text = string.Join(" · ", odd);
                _groupLine.color = Muted;
            }
        }

        /// <summary>Отказал или колеблется — не согласен с приказом.</summary>
        private static bool Disagrees(Warrior w)
        {
            var d = w.GetComponent<AOSWarriorWrapper>();
            if (d == null || d.LastContext == null) return false;
            return d.LastDecisionDetail.RefusedCommand || d.LastDecisionDetail.Hesitated;
        }

        /// <summary>Сколько — словом: «пятеро», «один». Чисел о душах нет (42-INTERFACE §2).</summary>
        private static string Many(int n)
        {
            switch (n)
            {
                case 1: return Loc.T("один");
                case 2: return Loc.T("двое");
                case 3: return Loc.T("трое");
                case 4: return Loc.T("четверо");
                case 5: return Loc.T("пятеро");
                case 6: return Loc.T("шестеро");
                case 7: return Loc.T("семеро");
                case 8: return Loc.T("восьмеро");
                case 9: return Loc.T("девятеро");
                default: return Loc.T("многие");
            }
        }

        // ---------- руки Греховода ----------

        private static readonly Color HandOn = new Color(0.227f, 0.173f, 0.133f, 1f);
        private static readonly Color HandOff = new Color(0.13f, 0.10f, 0.08f, 1f);

        /// <summary>
        /// Две кнопки рук — F «Говорить», E «Забрать душу» (42-INTERFACE §3).
        /// Недоступное — тусклое (§1, п. 6): иначе игрок жмёт и ничего
        /// не происходит. «Говорить» горит, когда F заговорит с этим воином;
        /// «Забрать душу» — когда душа в досягаемости жатвы и есть пустая банка.
        /// </summary>
        private void Hands(Warrior who)
        {
            var me = SinbinderPlayer.Instance;
            bool hero = who is SinbinderPlayer;

            bool talk = !hero && me != null
                     && CampFocus.GroundDistance(SinbinderPlayer.Where, who.transform.position) <= GearPanel.TalkReach;

            var souls = SoulManager.Instance;
            bool harvest = me != null && souls != null && souls.InReach(SinbinderPlayer.Where)
                        && Satchel.FreeJar() >= 0;

            // Слова — при показе: язык меняют в меню паузы.
            Set(_handsTitle, Loc.T("РУКИ ГРЕХОВОДА"), Muted);
            if (_talkText != null) _talkText.text = Loc.T("Говорить");
            if (_harvestText != null) _harvestText.text = Loc.T("Забрать душу");

            Hand(_talkPlate, _talkText, talk);
            Hand(_harvestPlate, _harvestText, harvest);

            if (_handsHint == null) return;
            _handsHint.color = Faint;
            _handsHint.text = hero ? Loc.T("Подойдите к воину, чтобы говорить")
                            : talk ? Loc.T("В разговоре: как он к вам · отдать долг · снаряжение")
                            : Loc.T("Подойдите ближе, чтобы говорить");
        }

        private static void Hand(Image plate, Text text, bool on)
        {
            if (plate != null) plate.color = on ? HandOn : HandOff;
            if (text != null) text.color = on ? Bone : Faint;
        }

        /// <summary>
        /// Кого показывать. Из выделенных воинов — того, кто громче всех
        /// не согласен (42-INTERFACE §3, «Группа»): сперва отказавшего, потом
        /// колеблющегося, иначе первого. Греховода — только если выделен он
        /// один: рамка берёт и его (26 сентября), и панель с героем вместо
        /// воина врала бы, о ком речь.
        /// </summary>
        private static Warrior Current()
        {
            Warrior first = null, hesitant = null;
            foreach (var w in Picked())
            {
                var d = w.GetComponent<AOSWarriorWrapper>();
                if (d != null && d.LastContext != null)
                {
                    if (d.LastDecisionDetail.RefusedCommand) return w;
                    if (hesitant == null && d.LastDecisionDetail.Hesitated) hesitant = w;
                }
                if (first == null) first = w;
            }
            if (hesitant != null) return hesitant;
            if (first != null) return first;

            var manager = SelectionManager.Instance;
            if (manager == null) return null;
            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var hero = unit.GetComponentInParent<Warrior>();
                if (hero is SinbinderPlayer) return hero;
            }
            return null;
        }

        /// <summary>Выделенные воины, без Греховода, по порядку выделения.</summary>
        private static System.Collections.Generic.List<Warrior> Picked()
        {
            var list = new System.Collections.Generic.List<Warrior>();
            var manager = SelectionManager.Instance;
            if (manager == null) return list;

            foreach (var unit in manager.GetSelectedUnits())
            {
                if (unit == null) continue;
                var w = unit.GetComponentInParent<Warrior>();
                if (w == null || w is SinbinderPlayer || list.Contains(w)) continue;
                list.Add(w);
            }
            return list;
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
                // Середина зоны не пустует (разбор кадра 1 октября): крупно —
                // что он делает, тише — что в нём сейчас никто не спорит.
                string doing = PhraseGenerator.Doing(decision.Action);
                Voice(null, string.IsNullOrEmpty(doing) ? "" : char.ToUpper(doing[0]) + doing.Substring(1),
                      Grammar.For(who.Gender, Loc.T("ничто в нём сейчас не спорит с приказом")), Muted, 30);
                Order(Loc.T("Ваш приказ — громче всех"),
                      Grammar.For(who.Gender, Loc.T("громче всех его голосов")), true);
                return;
            }

            string module = Speaker(decision);
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

            // Голосов у него нет — есть вещи (42-INTERFACE §3, «Греховод»):
            // сума по банкам словами и кошель числом (§2: числом — только то,
            // чем Греховод владеет и что тратит).
            Set(_voicesTitle, Loc.T("Сума и кошель"), Bone);

            if (_satchelText != null)
            {
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < Satchel.Size; i++)
                {
                    var slot = Satchel.At(i);
                    if (slot.Empty) continue;
                    if (lines.Length > 0) lines.Append('\n');
                    lines.Append(i == Satchel.Selected ? "▸ " : "   ").Append(Satchel.Describe(i));
                }
                _satchelText.text = lines.Length > 0 ? lines.ToString() : Loc.T("Сума пуста");
                _satchelText.color = Bone;
            }

            if (_purseText != null)
            {
                var purse = Inventory.PlayerInventory.Instance;
                _purseText.text = purse != null
                    ? Loc.F("Кошель: {0}\n\nR — в руку и обратно\nTab — следующая банка", purse.Gold)
                    : Loc.T("R — в руку и обратно\nTab — следующая банка");
                _purseText.color = Muted;
            }
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

        /// <summary>
        /// Чей голос назвать — тот, чьи слова стоят строкой ниже
        /// (<see cref="PhraseGenerator.Reason"/>): одно решение — одни слова.
        ///
        /// Громче всех за поступок голосует <see cref="Decision.TopModule"/> —
        /// за «остаться на месте» это обычно уныние. Но приказ проиграл не ему,
        /// а причине «от противного» (<see cref="Counterfactual"/>), и строка
        /// ниже — её. В кадре прогона 2 октября так и вышло: «Уныние говорит
        /// громко — ему не платили третью вылазку подряд». Поэтому: решил голос
        /// души — он; решила причина — голос, которому она принадлежит
        /// (долг, карман, добыча, вещь — жадность; усталость, скучный обход —
        /// уныние; раны, обступили — страх; свой в беде — совесть); даль —
        /// тот, кто её слышит, то есть громкий.
        /// </summary>
        private static string Speaker(Decision d)
        {
            if (!d.Weighed) return d.TopModule;
            if (!string.IsNullOrEmpty(d.DecisiveVoice)) return d.DecisiveVoice;

            switch (d.Decisive)
            {
                case Counterfactual.Factor.Debt:
                case Counterfactual.Factor.Pocket:
                case Counterfactual.Factor.Loot:
                case Counterfactual.Factor.Temptation:   return "Greed";
                case Counterfactual.Factor.Fatigue:
                case Counterfactual.Factor.Patrol:       return "Sloth";
                case Counterfactual.Factor.Wounds:
                case Counterfactual.Factor.Surrounded:   return "Fear";
                case Counterfactual.Factor.AllyInDanger: return "Morality";
                default:                                 return d.TopModule;
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
