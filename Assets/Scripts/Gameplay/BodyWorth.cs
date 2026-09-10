// Assets/Scripts/Gameplay/BodyWorth.cs
using UnityEngine;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    /// <summary>
    /// Чего стоит труп.
    ///
    /// Раньше здесь стоял <c>Random.Range(5, 20)</c> на каждую смерть,
    /// и случайность оттуда доходила до титулов: важность деяния
    /// считается как золото/10, <c>TitleManager</c> складывает важность
    /// и сравнивает с порогом, а титул — один из рычагов игрока.
    /// Два прогона с одними и теми же душами давали разные имена.
    /// Правило «одинаковый вход обязан давать одинаковый выход»
    /// не держалось ровно там, где его легче всего заметить.
    ///
    /// Теперь труп стоит того, чем покойник был при жизни:
    ///
    /// * оболочка — сколько на нём вообще могло быть надето;
    /// * грех — сколько он при себе держал;
    /// * мораль — обирал ли он мёртвых до того, как лечь рядом;
    /// * уровень — сколько успел походить.
    ///
    /// Это не только про повторяемость. Четыре вида охотников в прологе
    /// отличаются грехом — значит, теперь отличаются и добычей: с Ловчего
    /// (Жадность) снимают заметно больше, чем с Мясника (Чревоугодие,
    /// всё сожрал при жизни). Кого бить первым — становится вопросом,
    /// а не жребием.
    ///
    /// Правится замером: <c>Tools/bench → ДОБЫЧА</c>.
    /// </summary>
    public static class BodyWorth
    {
        // Сколько на оболочке может быть надето вообще. Призрак — ноль,
        // и это не «мало», а «нечего брать»: тела нет.
        //
        // Зомби — 10.4, а не круглое 11: это подгонка по замеру, а не
        // на глаз. Все трупы, которые игрок разбирает в прологе, — зомби
        // (HunterSquadSpawner), и средняя добыча за труп обязана остаться
        // прежней, 12 золота. Иначе важность деяния (золото/10) поплывёт,
        // и титулы придут раньше или позже, чем раньше приходили.
        // При 10.4 средняя равна прежней ровно: жребий убран, а пороги
        // в TitleDatabase не сдвинулись ни на один труп.
        // Проверяется: Tools/bench → ДОБЫЧА.
        private const float GhostBase    = 0f;
        private const float SkeletonBase = 8f;
        private const float ZombieBase   = 10.4f;
        private const float GolemBase    = 16f;

        // Границы доли греха. Без них предельно жадный святой голем
        // уезжает в такие числа, что титул берётся с одного трупа.
        private const float MinShare = 0.35f;
        private const float MaxShare = 1.70f;

        // Прибавка за уровень. Не «сильнее», а «дольше ходил»:
        // на старом охотнике больше нажитого.
        private const float RankStep = 0.15f;
        private const float MaxRank  = 1.75f;

        /// <summary>С какого спектра начинается ношеный трофей.</summary>
        private const float EquipmentThreshold = 40f;

        /// <summary>Сколько золота осталось на трупе.</summary>
        public static int Gold(ShellType shell, SoulData soul)
        {
            if (soul == null)
            {
                // Отсутствие души — событие, а не ноль: труп без покойника
                // означает, что кто-то собрал добычу мимо этой правды.
                Debug.LogWarning("[ДОБЫЧА] Труп без души: считаю только по оболочке.");
                return Mathf.RoundToInt(ShellBase(shell));
            }

            float worth = ShellBase(shell)
                        * SinShare(soul)
                        * MoralShare(soul.Moral)
                        * RankShare(soul.Level);

            return Mathf.Max(0, Mathf.RoundToInt(worth));
        }

        /// <summary>
        /// Осталось ли на трупе снаряжение.
        ///
        /// Трофей носит тот, кому он что-то значил: гордый — свой,
        /// завистливый — чужой. Остальные его теряли, проедали
        /// или не наклонялись поднять.
        /// </summary>
        public static bool HasEquipment(ShellType shell, SoulData soul)
        {
            if (shell == ShellType.Ghost) return false;   // нечему остаться
            if (soul == null) return false;

            float pride = soul.Get(SinType.Pride);
            float envy = soul.Get(SinType.Envy);
            return Mathf.Max(pride, envy) >= EquipmentThreshold;
        }

        /// <summary>
        /// Что именно осталось. Название читается в журнале и в описи,
        /// поэтому оно тоже обязано быть одним и тем же при одном входе.
        /// </summary>
        public static string Equipment(ShellType shell, SoulData soul)
        {
            if (!HasEquipment(shell, soul)) return null;
            return soul.Get(SinType.Envy) > soul.Get(SinType.Pride)
                 ? "Чужой клинок"
                 : "Родовой клинок";
        }

        // ──────────────────────────────────
        // Составляющие
        // ──────────────────────────────────

        private static float ShellBase(ShellType shell)
        {
            switch (shell)
            {
                case ShellType.Golem:    return GolemBase;
                case ShellType.Zombie:   return ZombieBase;
                case ShellType.Skeleton: return SkeletonBase;
                default:                 return GhostBase;
            }
        }

        /// <summary>
        /// Тяга греха к нажитому. Единица — «ничего не прибавил и ничего
        /// не растерял»; больше единицы копят, меньше — не держат.
        /// </summary>
        private static float Hoard(SinType sin)
        {
            switch (sin)
            {
                case SinType.Greed:    return 1.60f;   // копил
                case SinType.Envy:     return 1.30f;   // носил чужое
                case SinType.Pride:    return 1.15f;   // держал вид
                case SinType.Lust:     return 1.05f;   // подарки
                case SinType.Wrath:    return 0.85f;   // ломал своё же
                case SinType.Gluttony: return 0.75f;   // проел
                default:               return 0.60f;   // Уныние: не поднимал
            }
        }

        /// <summary>
        /// Доля от оболочки по греху.
        ///
        /// Интенсивность спектра берётся со знаком: добродетель —
        /// отрицательная половина той же шкалы, поэтому щедрый труп
        /// беден ровно настолько, насколько жадный богат, и второй
        /// таблицы для добродетелей не нужно.
        /// </summary>
        private static float SinShare(SoulData soul)
        {
            float pull = Hoard(soul.Sin) - 1f;
            float force = Mathf.Clamp(soul.SinIntensity, -100f, 100f) / 100f;
            return Mathf.Clamp(1f + pull * force, MinShare, MaxShare);
        }

        private static float MoralShare(MoralType moral)
        {
            switch (moral)
            {
                case MoralType.Vicious: return 1.10f;   // обирал мёртвых
                case MoralType.Pious:   return 0.90f;   // не обирал
                default:                return 1.00f;
            }
        }

        private static float RankShare(int level)
        {
            return Mathf.Clamp(1f + (level - 1) * RankStep, 1f, MaxRank);
        }
    }
}
