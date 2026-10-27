// Assets/Scripts/Gameplay/Signature.cs
// Перевод: текст через Loc
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Приметы девяти словами. Автор, 26 октября: «любой Игрок, даже самый слепой
    /// и тупой, должен понять — каждый уникален… сейчас Игрок не познакомится
    /// с каждой пешкой — он просто их потеряет, так и не узнав их особенностей».
    /// Тексты — облачная (28-ORDERS, «Ответы облачной», пп. 1–3; черновик,
    /// автор смотрит в игре).
    ///
    /// Три строки на каждого:
    /// <list type="bullet">
    /// <item><b>почему так зовут</b> — под именем на панели выделенного
    ///   (<see cref="UI.SelectedUnitPanelUI"/>);</item>
    /// <item><b>первая фраза</b> — вместо приветствия в первом разговоре
    ///   с Греховодом (<see cref="FirstLine"/>), один раз за игру;</item>
    /// <item><b>гибель</b> — строкой журнала: смерть помнит примету
    ///   (<see cref="SignatureDeaths"/>).</item>
    /// </list>
    /// Ключ — имя без титулов (<see cref="Warrior.DisplayName"/>), как у <c>SquadRoster</c>.
    /// </summary>
    public static class Signature
    {
        private struct Lines { public string Why, First, Death; }

        private static readonly Dictionary<string, Lines> Table = new()
        {
            [Loc.N("Карган Старый Ворон")] = new Lines
            {
                Why = Loc.N("Старше всех в отряде. Ворон прибился к нему ещё при жизни и не ушёл после смерти."),
                First = Loc.N("Ворона не гоните, владыка. Он со мной дольше, чем вы."),
                Death = Loc.N("Ворон сидит у огня один."),
            },
            [Loc.N("Вейн Тихий")] = new Lines
            {
                Why = Loc.N("Говорит шёпотом и читает при свече, пока лагерь спит."),
                First = Loc.N("Тише, владыка. Свеча гаснет от громких слов."),
                Death = Loc.N("В палатке Вейна догорает свеча. Задуть её некому."),
            },
            [Loc.N("Марга Копатель")] = new Lines
            {
                Why = Loc.N("Копает везде, где встаёт лагерь, — и где-нибудь да находит. Найденное считает своим."),
                First = Loc.N("Земля всегда что-то прячет, владыка. Надо только не лениться."),
                Death = Loc.N("Копать теперь некому."),
            },
            [Loc.N("Брат Хальд")] = new Lines
            {
                Why = Loc.N("Бывший брат обители. Варит у огня снадобья и верит в них больше, чем в богов."),
                First = Loc.N("Не трогайте котелок, владыка. Это не похлёбка."),
                Death = Loc.N("Котелок Хальда выкипел. Что он варил, так никто и не узнал."),
            },
            [Loc.N("Одноглазый Хорь")] = new Lines
            {
                Why = Loc.N("Одним глазом видит больше, чем другие двумя, — особенно чужое. Носит знамя «Крыс»."),
                First = Loc.N("Глаз я отдал за кошелёк, владыка. Кошелёк того стоил."),
                Death = Loc.N("Гурт стоит над братом и не уходит."),
            },
            [Loc.N("Толстый Ю")] = new Lines
            {
                Why = Loc.N("Ест, даже когда есть уже нечем: у котла его найдёшь всегда."),
                First = Loc.N("Сядьте, владыка, похлёбка ещё тёплая. Хуже от неё не станет."),
                Death = Loc.N("У котла пусто. Похлёбка стынет."),
            },
            [Loc.N("Лиска")] = new Lines
            {
                Why = Loc.N("Хитёр и неслышен, как лис, а теперь ещё и ходит сквозь стены. Чужое пересчитывает — на всякий случай."),
                First = Loc.N("Я знаю, сколько у вас в сундуке, владыка. Не брал — считал."),
                Death = Loc.N("Чужое лежит непересчитанным."),
            },
            [Loc.N("Немой Гурт")] = new Lines
            {
                Why = Loc.N("Не говорит ни с кем, кроме брата, — и с братом тоже молча. Ходит за Хорем тенью."),
                First = null,   // немой: жест (Dialogue.CampLines.Gesture)
                Death = Loc.N("Хорь оглядывается, а за спиной никого."),
            },
            [Loc.N("Косой Ждан")] = new Lines
            {
                Why = Loc.N("Лучник, который бьёт мимо и всякий раз говорит, что метил не туда."),
                First = Loc.N("В шишку не попал — так я и не в шишку целился, владыка."),
                Death = Loc.N("Шишки на соснах висят целыми."),
            },
        };

        private static readonly HashSet<string> Greeted = new();

        /// <summary>
        /// Приметы рождённых генератором (<see cref="Core.WarriorGenerator"/>): строки
        /// уже сложены на языке игры. Девять ручных — в <see cref="Table"/>.
        /// </summary>
        private static readonly Dictionary<string, Lines> Born = new();

        /// <summary>Примета сгенерированного: почему так зовут, первая фраза, гибель.</summary>
        public static void Give(string name, string why, string first, string death)
        {
            if (string.IsNullOrEmpty(name)) return;
            Born[name] = new Lines { Why = why, First = first, Death = death };
        }

        private static bool Find(Warrior w, out Lines l, out bool born)
        {
            l = default;
            born = false;
            if (w == null) return false;
            var key = w.DisplayName ?? "";
            if (Table.TryGetValue(key, out l)) return true;
            born = Born.TryGetValue(key, out l);
            return born;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Greeted.Clear();

        /// <summary>Новая игра: первые фразы — снова первые.</summary>
        public static void Forget() => Greeted.Clear();

        /// <summary>Почему так зовут — строкой под именем; нет примет — пусто.</summary>
        public static string Why(Warrior w)
            => Find(w, out var l, out bool born) && l.Why != null ? (born ? l.Why : Loc.T(l.Why)) : "";

        /// <summary>Строка о гибели; нет — пусто.</summary>
        public static string Death(Warrior w)
            => Find(w, out var l, out bool born) && l.Death != null ? (born ? l.Death : Loc.T(l.Death)) : "";

        /// <summary>
        /// Первый разговор с Греховодом — его фраза вместо приветствия, один раз
        /// за игру; дальше — null, и он здоровается как всегда. Зовёт разговор
        /// (<see cref="UI.GearPanel"/>). Немой Гурт фразы не имеет — здоровается жестом,
        /// как всегда.
        /// </summary>
        public static string FirstLine(Warrior w)
        {
            if (w == null || w is SinbinderPlayer) return null;
            if (!Find(w, out var l, out bool born) || l.First == null) return null;
            if (!Greeted.Add(w.DisplayName ?? "")) return null;
            Debug.Log($"[ПРИМЕТА] первая фраза {w.DisplayName}");
            return born ? l.First : Loc.T(l.First);
        }
    }
}
