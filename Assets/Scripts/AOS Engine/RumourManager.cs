// Assets/Scripts/AOS Engine/RumourManager.cs
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Что один воин слышал о другом.
    ///
    /// Слух копится от повторения: одно деяние — молва, три — репутация.
    /// Растёт быстрее у тех, кто и так расположен к герою: своим верят
    /// охотнее.
    ///
    /// Система написана давно и не вызывалась ни разу. Разбор дефектов —
    /// в docs/12-BALANCE.md; здесь они исправлены. Главный: слухи было
    /// нечем прочитать — хранилище приватное, наружу не выходило ничего,
    /// так что система только писала.
    ///
    /// Намеренно не подключена к голосованию. Это был бы новый повод
    /// у воина, а сторона воина заморожена до первого прохождения демо
    /// (см. docs/05-BOUNDS.md, четвёртый вопрос к добавлению).
    /// </summary>
    public static class RumourManager
    {
        /// <summary>Слух считается подтверждённым при этом значении.</summary>
        public const float ConfirmAt = 100f;

        /// <summary>Насколько охотнее верят тому, кто нравится.</summary>
        public const float TrustBonus = 1.5f;

        /// <summary>Ключ — слушатель и герой; на пару приходится один слух.</summary>
        private static readonly Dictionary<(string listener, string subject), Rumour> _rumours = new();

        /// <summary>
        /// Слушатель узнаёт о деянии героя.
        ///
        /// Прежде здесь хранился список слухов под ключом, который уже
        /// содержал обоих: список не мог вместить больше одной записи,
        /// а второе деяние молча дописывалось в первый слух — и текст
        /// продолжал рассказывать о первом. Теперь текст обновляется
        /// вместе с деянием.
        /// </summary>
        public static void Spread(Warrior listener, Warrior hero, DeedType deed, float value)
        {
            if (listener == null || hero == null || listener == hero) return;
            if (listener.Team != hero.Team) return;

            // Отношения вычисляются из памяти, и до первого воспоминания
            // системы может не быть вовсе. Отсутствие — не повод падать.
            float relationship = listener.Relationships != null
                ? listener.Relationships.GetRelationship(listener.Id, hero.Id)
                : 0f;

            // Тому, кого не выносишь, не верят.
            if (relationship < -50f) return;

            var key = (listener.Id, hero.Id);
            if (!_rumours.TryGetValue(key, out var rumour))
            {
                rumour = new Rumour { SubjectId = hero.Id };
                _rumours[key] = rumour;
            }

            rumour.Text = $"{hero.DisplayName} — {DeedName(deed)}";
            rumour.Progress += Mathf.Max(0f, value) * (relationship > 50f ? TrustBonus : 1f);

            if (rumour.Progress >= ConfirmAt) rumour.Confirmed = true;
        }

        /// <summary>Что этот воин слышал о том. Может вернуть null.</summary>
        public static Rumour What(Warrior listener, Warrior hero)
        {
            if (listener == null || hero == null) return null;
            return _rumours.TryGetValue((listener.Id, hero.Id), out var r) ? r : null;
        }

        /// <summary>Верит ли слушатель в молву о герое.</summary>
        public static bool Believes(Warrior listener, Warrior hero)
            => What(listener, hero)?.Confirmed == true;

        /// <summary>Всё, что слышали об этом воине. Для экрана отряда.</summary>
        public static List<Rumour> About(Warrior hero)
        {
            var found = new List<Rumour>();
            if (hero == null) return found;

            foreach (var pair in _rumours)
                if (pair.Key.subject == hero.Id) found.Add(pair.Value);
            return found;
        }

        /// <summary>
        /// Сколько людей в отряде уже верят молве. Это и есть та величина,
        /// на которой позже можно строить титул или голос модуля.
        /// </summary>
        public static int BelieverCount(Warrior hero)
        {
            int n = 0;
            foreach (var r in About(hero)) if (r.Confirmed) n++;
            return n;
        }

        /// <summary>
        /// Забыть всё. Хранилище статическое и между сценами не чистилось:
        /// слухи прошлого боя переживали загрузку следующего.
        /// </summary>
        public static void Clear() => _rumours.Clear();

        /// <summary>Деяние по-русски. Игрок не должен видеть имён из кода.</summary>
        public static string DeedName(DeedType deed)
        {
            switch (deed)
            {
                case DeedType.Kill:             return "убийца";
                case DeedType.KillCommander:    return "тот, кто свалил командира";
                case DeedType.SaveAlly:         return "вытащил своего";
                case DeedType.ProtectCommander: return "прикрыл командира";
                case DeedType.CollectMostLoot:  return "унёс больше всех";
                case DeedType.FindTreasure:     return "нашёл клад";
                case DeedType.DigMostSouls:     return "выкопал больше всех душ";
                case DeedType.SurviveMission:   return "вернулся живым";
                case DeedType.LastStand:        return "стоял до последнего";
                case DeedType.NeverRetreat:     return "не отступал ни разу";
                case DeedType.Escape:           return "ушёл, когда все легли";
                case DeedType.ExecuteEnemy:     return "добил пленного";
                case DeedType.RecruitWarrior:   return "привёл нового";
                default:                        return "чем-то отличился";
            }
        }
    }
}
