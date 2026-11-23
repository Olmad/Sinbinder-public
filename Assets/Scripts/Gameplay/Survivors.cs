// Assets/Scripts/Gameplay/Survivors.cs
// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>Кто пережил встречу с отрядом на вылазке. Идёт в сохранение.</summary>
    [Serializable]
    public sealed class SavedSurvivor
    {
        public string Name;
        /// <summary>Где ждёт: Id карты личной вылазки.</summary>
        public string Place;
        public int Sin;
        public bool Undead;
        /// <summary>Доля здоровья, с которой ушёл.</summary>
        public float Health = 1f;
        public int Meetings;
        /// <summary>Кого из отряда убил — через «, », в порядке смертей.</summary>
        public string Killed = "";
        public bool Fled;
        /// <summary>Почему он здесь: «bound» — покойник места, «caravan» — охрана обоза, «order» — дозор Ордена.</summary>
        public string Reason = "";
    }

    /// <summary>
    /// Время мира в походах: каждый ушедший отряд и каждое возвращение Греховода —
    /// один шаг. Своих часов у мира нет (23.11), а «через пару минут» и «через три
    /// похода» должны давать разное: автор — «логично ли, что часовню разграбили,
    /// а она восстановилась через пару минут?». Идёт в сохранение.
    /// </summary>
    public static class Sorties
    {
        public static int Count { get; private set; }
        public static void Tick() => Count++;
        public static void Restore(int count) => Count = Math.Max(0, count);
        public static void Forget() => Count = 0;
    }

    /// <summary>С чем возвращаются на место, где уже были.</summary>
    public enum Return { First, SameNight, Later, Reoccupied }

    /// <summary>
    /// Выжившие помнят (автор, 23.11: «выжившие стражники сохранялись в памяти
    /// и могли оказаться в другой экспедиции. НО ЛОГИЧНО! С объяснением почему
    /// так… подражать Middle-earth: Shadow of War»). План 69 §7.
    ///
    /// Шаг 1 — тот же враг на том же месте и по названной причине:
    /// покойник привязан к месту, где его подняли, и кости у него не срастаются;
    /// охрана обоза ходит той же дорогой. Причину игра говорит словами при входе,
    /// а сам выживший — при встрече, по тому, что было. Переходы между вылазками
    /// (шаг 2) — только живым и только с поводом.
    ///
    /// Правило без Unity — проверяется стендом.
    /// </summary>
    public static class Survivors
    {
        private static readonly List<SavedSurvivor> _all = new List<SavedSurvivor>();

        public static IReadOnlyList<SavedSurvivor> All => _all;

        // Имена — мужские: узнавание и объяснения пока только в мужском роде,
        // а женщина, о которой сказано «он», — та же ошибка, что «ему» у Марги.
        private static readonly string[] _dead =
        {
            Loc.N("Мирон Звонарь"), Loc.N("Ефрем Сухорукий"), Loc.N("Антип Свечник"),
            Loc.N("Лука Безбородый"), Loc.N("Прохор Немытый"), Loc.N("Устин Вдовец"),
            Loc.N("Фома Колодезник"), Loc.N("Савва Кривошей"),
        };
        private static readonly string[] _living =
        {
            Loc.N("Бажен Рыжий"), Loc.N("Тихон Соляной"), Loc.N("Ерёма Щербатый"),
            Loc.N("Радим Копьё"), Loc.N("Неждан Хмурый"), Loc.N("Гордей Плечистый"),
            Loc.N("Малюта Быстрый"), Loc.N("Любим Кистень"),
        };

        // Дозор Ордена — братья, не наёмники обоза: «Тихон Соляной» у алтаря читался чужим (кадр 23.11).
        private static readonly string[] _order =
        {
            Loc.N("Брат Евсей"), Loc.N("Брат Корнил"), Loc.N("Брат Игнат"), Loc.N("Брат Аким"),
            Loc.N("Брат Пимен"), Loc.N("Брат Варлаам"), Loc.N("Брат Сысой"), Loc.N("Брат Феогност"),
        };

        /// <summary>Имя по тому, кто он: «bound», «caravan» или «order».</summary>
        public static string NameFor(string reason, ICollection<string> taken)
            => reason == "order" ? Pick(_order, taken) : NameFor(reason == "bound", taken);

        private static string Pick(string[] pool, ICollection<string> taken)
        {
            foreach (var n in pool)
                if ((taken == null || !taken.Contains(n)) && !_all.Exists(s => s.Name == n)) return n;
            return pool[(taken?.Count ?? 0) % pool.Length];
        }

        /// <summary>Кто ждёт на этом месте — в порядке, в каком их запомнили.</summary>
        public static List<SavedSurvivor> At(string place)
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all) if (s.Place == place) list.Add(s);
            return list;
        }

        /// <summary>
        /// Имя новому стражу. Не повторяет ни живых в памяти, ни уже взятых
        /// на этой карте: два Мирона Звонаря прочитались бы как ошибка сборки.
        /// Порядок неизменен — тот же поход даёт те же имена.
        /// </summary>
        public static string NameFor(bool undead, ICollection<string> taken)
        {
            var pool = undead ? _dead : _living;
            foreach (var n in pool)
            {
                if (taken != null && taken.Contains(n)) continue;
                if (_all.Exists(s => s.Name == n)) continue;
                return n;
            }
            return pool[(taken?.Count ?? 0) % pool.Length];
        }

        /// <summary>
        /// Итог встречи на месте: живые записаны (кто уже был — дополнен),
        /// павших больше нет. Зовётся при выходе из вылазки.
        /// </summary>
        public static void Settle(string place, IList<SavedSurvivor> alive)
        {
            _all.RemoveAll(s => s.Place == place);
            if (alive == null) return;
            foreach (var s in alive)
                if (s != null && !string.IsNullOrEmpty(s.Name)) { s.Place = place; _all.Add(s); }
        }

        // ── Переходы (план 69 §7 п.2): только живые и только с поводом ──

        /// <summary>Место бродяг: охранник разграбленного обоза, которому больше некого охранять.</summary>
        public const string Road = "road";

        /// <summary>Обоз разграблен — выжившая охрана без места: уходит на дорогу.</summary>
        public static void Wander(IList<SavedSurvivor> robbed)
        {
            if (robbed == null) return;
            foreach (var s in robbed)
                if (s != null && !s.Undead && !string.IsNullOrEmpty(s.Name))
                { s.Place = Road; s.Reason = "robbed"; _all.Add(s); }
        }

        /// <summary>
        /// Дозор Ордена берёт с собой того, кто знает Греховода в лицо: охранника
        /// разграбленного обоза. Он переходит на новое место; нет такого — null.
        /// </summary>
        public static SavedSurvivor Guide(string place)
        {
            var s = _all.Find(x => x.Place == Road && x.Reason == "robbed");
            if (s == null) return null;
            s.Place = place; s.Reason = "guide";
            return s;
        }

        /// <summary>
        /// Бежавший живой возвращается не один — если прошло время (не той же ночью).
        /// Покойник места не покидает и никого не приводит. Привёл — долг снят.
        /// </summary>
        public static List<SavedSurvivor> BringFriends(IList<SavedSurvivor> at, Return back)
        {
            var list = new List<SavedSurvivor>();
            if (back != Return.Later || at == null) return list;
            foreach (var s in at) if (s != null && s.Fled && !s.Undead) { list.Add(s); s.Fled = false; }
            return list;
        }

        /// <summary>Строка журнала: бежавший вернулся с подмогой.</summary>
        public static string Friends(SavedSurvivor s)
            => Loc.F("{0} бежал от вас в прошлый раз — и вернулся не один.", Loc.Name(s.Name));

        /// <summary>Через сколько походов разграбленную часовню займут снова.</summary>
        public const int ReoccupyAfter = 3;

        /// <summary>
        /// Место не обновляется само. Вернулся, пока в мире не прошло ни одного похода, —
        /// та же ночь: раны у всех те же. Зачищенное пустует; разграбленную часовню
        /// через <see cref="ReoccupyAfter"/> похода займёт дозор Ордена — слух дошёл.
        /// Зачищенное, но не разграбленное — пустует: беречь там нечего.
        /// </summary>
        public static Return OnReturn(int leftAt, int clearedAt, bool plundered, int enemiesLeft, int now)
        {
            if (leftAt < 0) return Return.First;
            if (enemiesLeft == 0 && plundered && clearedAt >= 0 && now - clearedAt >= ReoccupyAfter)
                return Return.Reoccupied;
            return now <= leftAt ? Return.SameNight : Return.Later;
        }

        /// <summary>Строка журнала: вернулись той же ночью.</summary>
        public static string SameNight() => Loc.T("Вы вернулись той же ночью: стража не успела ни уйти, ни залечить раны.");

        /// <summary>Строка журнала: часовню заняли снова.</summary>
        public static string Reoccupied() => Loc.T("Слух о разграбленной часовне дошёл до Ордена: у алтаря теперь их дозор.");

        /// <summary>Почему он здесь — строка журнала при входе.</summary>
        public static string Why(SavedSurvivor s, float maxHp)
        {
            if (s.Reason == "guide")
                return Loc.F("{0} пришёл с дозором Ордена: обоз, который он охранял, разграбили, и теперь он служит Ордену — он знает вас в лицо.", Loc.Name(s.Name));
            if (s.Reason == "order")
                return Loc.F("{0} — из дозора Ордена: часовню стерегут с тех пор, как её разграбили.", Loc.Name(s.Name))
                       + " " + Deed(s, maxHp);
            string why = s.Undead
                ? Loc.F("{0} ждёт здесь: покойник не может уйти от места, где его подняли.", Loc.Name(s.Name))
                : Loc.F("{0} снова в охране: обоз ходит той же дорогой и с теми же людьми.", Loc.Name(s.Name));
            return why + " " + Deed(s, maxHp);
        }

        private static string Deed(SavedSurvivor s, float maxHp)
        {
            if (!string.IsNullOrEmpty(s.Killed))
                // Имя — после двоеточия, в именительном: «убил Вейна» потребовало бы
                // склонять имена, а «пал/пала» — знать род павшего.
                return Loc.F("В прошлый раз от его руки погибли ваши: {0}.", s.Killed);
            if (s.Fled) return Loc.T("В прошлый раз он бежал от отряда.");
            if (s.Undead && s.Health < 0.999f)
                return Loc.F("Кости с прошлой встречи не срослись: {0:F0} из {1:F0}.", s.Health * maxHp, maxHp);
            return Loc.T("В прошлый раз он видел Греховода и остался жив.");
        }

        /// <summary>Что он говорит, узнав Греховода.</summary>
        public static string Greeting(SavedSurvivor s)
        {
            if (!string.IsNullOrEmpty(s.Killed))
            {
                string first = s.Killed.Split(new[] { ", " }, StringSplitOptions.None)[0];
                return s.Undead ? Loc.F("Опять ты. {0} так и лежит у часовни.", first)
                                : Loc.F("Помню тебя. Твоих я уже считаю: {0}.", first);
            }
            if (s.Reason == "guide") return Loc.T("Обоз ты разграбил. Теперь я служу Ордену — и тебя узнаю где угодно.");
            if (s.Fled) return Loc.T("Ты. В тот раз я ушёл. Больше не уйду.");
            if (s.Undead && s.Health < 0.999f) return Loc.T("Кости не срастаются, некромант. Я считаю каждую.");
            return Loc.T("Я тебя видел. Ты ушёл — я остался.");
        }

        public static List<SavedSurvivor> Snapshot()
        {
            var list = new List<SavedSurvivor>();
            foreach (var s in _all)
                list.Add(new SavedSurvivor { Name = s.Name, Place = s.Place, Sin = s.Sin, Undead = s.Undead,
                    Health = s.Health, Meetings = s.Meetings, Killed = s.Killed ?? "", Fled = s.Fled, Reason = s.Reason ?? "" });
            return list;
        }

        public static void Restore(List<SavedSurvivor> saved)
        {
            _all.Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (s != null && !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Place)) _all.Add(s);
        }

        /// <summary>Новая игра — никто никого не помнит, и время с нуля.</summary>
        public static void Forget() { _all.Clear(); Sorties.Forget(); }
    }
}
