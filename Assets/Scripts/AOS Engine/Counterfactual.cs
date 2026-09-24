// Assets/Scripts/AOS Engine/Counterfactual.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Sinbinder.Core;
using Sinbinder.Inventory;

namespace Sinbinder.AOS
{
    /// <summary>
    /// Объяснение отказа «от противного» (docs/35-CRITIQUE.md §3, просьба
    /// автора 24 сентября).
    ///
    /// Прежнее объяснение называло самый громкий голос <b>за</b> победившее
    /// действие. Но многие причины голосуют <b>против приказа</b>, а не за
    /// что-то другое: даль, карман, долг. Победитель тогда — «стоять»,
    /// и за «стоять» громче всех кричит усталость: жадный остался стоять
    /// из-за кармана, а игрок читает «силы у него на исходе». За один день
    /// это латали дважды особыми правилами (голос, карман); третья такая
    /// причина снова соврала бы.
    ///
    /// Здесь — одно правило вместо россыпи. Движок детерминирован,
    /// голосование дешёвое: для отказа голосование пересчитывается без
    /// каждой причины по очереди, и называется первая, без которой приказ
    /// был бы исполнен. Порядок — от того, что игрок исправит скорее всего:
    /// подойти, заплатить, забрать. Ни одна поодиночке не решает — значит,
    /// решил характер, и тогда второй круг: пересчёт без каждого голоса
    /// души (<see cref="DecisiveVoice"/>) — какой из грехов перевесил.
    /// Первая версия в этом случае брала самый громкий голос за победителя,
    /// и стенд показал ту же болезнь: у жадного, стоящего из-за кармана,
    /// звучало «у него не осталось воли» — голос уныния от усталости.
    ///
    /// Причина по построению не врёт: она названа, только если без неё
    /// воин послушался бы. Этим же и выполняется требование 3 из GDD —
    /// «отказ можно было предотвратить, и игрок это видит».
    ///
    /// Пересчёт здесь не делается: его даёт тот, кто голосует
    /// (<see cref="BehaviourResolver.WouldObey"/> в игре, свой голос
    /// у стенда). Так правило одно и на игру, и на прибор.
    ///
    /// Выключатель: до прогона выключено; «причина» в консоли (~).
    /// </summary>
    public static class Counterfactual
    {
        public static bool Enabled { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Rearm() => Enabled = false;

        /// <summary>Причины, которые можно убрать из положения по одной.</summary>
        public enum Factor
        {
            None,
            Distance,      // приказ издали (голос Греховода)
            Debt,          // не заплатили
            Pocket,        // есть что терять
            Temptation,    // вещь тянет в другую сторону
            Loot,          // добыча рядом
            Patrol,        // скучный приказ
            AllyInDanger,  // свой в беде
            Wounds,        // ранен
            Surrounded,    // обступили
            Fatigue,       // устал
        }

        /// <summary>
        /// Порядок проверки — он же порядок выбора, если решают несколько
        /// причин поодиночке: сперва то, что игрок исправит сам и сразу.
        /// </summary>
        private static readonly Factor[] Order =
        {
            Factor.Distance, Factor.Debt, Factor.Pocket, Factor.Temptation, Factor.Loot,
            Factor.Patrol, Factor.AllyInDanger, Factor.Wounds, Factor.Surrounded, Factor.Fatigue,
        };

        /// <summary>Есть ли эта причина в положении вообще.</summary>
        public static bool Present(DecisionContext c, Factor f)
        {
            switch (f)
            {
                case Factor.Distance:     return c.HasCommand && c.CommandVolume < 1f;
                case Factor.Debt:         return c.UnpaidMissions > 0;
                case Factor.Pocket:       return c.PocketGold > 0;
                case Factor.Temptation:   return Tempting(c) != null;
                case Factor.Loot:         return c.NearbyLoot > 0;
                case Factor.Patrol:       return c.CommandIsPatrol;
                case Factor.AllyInDanger: return c.AllyInDanger;
                case Factor.Wounds:       return c.MaxHP > 0f && c.CurrentHP < c.MaxHP * 0.6f;
                case Factor.Surrounded:   return c.Surrounded;
                case Factor.Fatigue:      return c.Fatigue > 0.2f || c.IsExhausted;
                default:                  return false;
            }
        }

        /// <summary>
        /// То же положение без одной причины. Копия: настоящее положение
        /// не трогается, списки не правятся на месте — заменяются.
        /// </summary>
        public static DecisionContext Without(DecisionContext c, Factor f)
        {
            var x = c.Copy();
            switch (f)
            {
                case Factor.Distance:     x.CommandVolume = 1f; break;
                case Factor.Debt:         x.UnpaidMissions = 0; break;
                case Factor.Pocket:       x.PocketGold = 0; break;
                case Factor.Loot:         x.NearbyLoot = 0; break;
                case Factor.Patrol:       x.CommandIsPatrol = false; break;
                case Factor.AllyInDanger: x.AllyInDanger = false; break;
                case Factor.Wounds:       x.CurrentHP = x.MaxHP; break;
                case Factor.Surrounded:   x.Surrounded = false; break;
                case Factor.Fatigue:      x.Fatigue = 0f; x.IsExhausted = false; break;
                case Factor.Temptation:
                    var plain = new List<InventoryItem>();
                    if (c.CarriedItems != null)
                        foreach (var item in c.CarriedItems)
                            if (item != null && Mathf.Approximately(item.TemptationValue, 0f)) plain.Add(item);
                    x.CarriedItems = plain;
                    break;
            }
            return x;
        }

        /// <summary>
        /// Первая причина, без которой приказ был бы исполнен. None — ни одна
        /// поодиночке не решает: решил характер. <paramref name="obeys"/> —
        /// «исполнил бы приказ в таком положении», считает голосующий.
        /// </summary>
        public static Factor Decisive(DecisionContext c, Func<DecisionContext, bool> obeys)
        {
            if (c == null || obeys == null || !c.HasCommand) return Factor.None;

            foreach (var f in Order)
                if (Present(c, f) && obeys(Without(c, f))) return f;
            return Factor.None;
        }

        /// <summary>
        /// Круг пар: две причины положения, без которых вместе приказ был бы
        /// исполнен, а без любой одной — нет. Отказ, который держится на двух
        /// причинах, — частый в бою: раненый рядом и силы на исходе. Пара
        /// тоже исправима игроком, только двумя шагами. Порядок — тот же:
        /// первая пара по порядку причин.
        /// </summary>
        public static bool DecisivePair(DecisionContext c, Func<DecisionContext, bool> obeys,
                                        out Factor first, out Factor second)
        {
            first = second = Factor.None;
            if (c == null || obeys == null || !c.HasCommand) return false;

            for (int i = 0; i < Order.Length; i++)
            {
                if (!Present(c, Order[i])) continue;
                var without = Without(c, Order[i]);
                for (int j = i + 1; j < Order.Length; j++)
                {
                    if (!Present(c, Order[j])) continue;
                    if (!obeys(Without(without, Order[j]))) continue;
                    first = Order[i];
                    second = Order[j];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Голоса души, которые стоит проверять во втором круге: те, что могут
        /// тянуть против приказа. Верность не в списке — она всегда за.
        /// Свой главный грех — первым: его игрок видит в панели словом.
        /// </summary>
        public static IEnumerable<string> Voices(SinType? own)
        {
            if (own.HasValue) yield return own.Value.ToString();
            foreach (var id in VoiceOrder)
                if (!own.HasValue || id != own.Value.ToString()) yield return id;
        }

        private static readonly string[] VoiceOrder =
        {
            "Greed", "Pride", "Wrath", "Envy", "Lust", "Gluttony", "Sloth",
            "Patience", "Fear", "Morality", "Memory", "Virtue",
        };

        /// <summary>
        /// Второй круг: первый голос души, без которого приказ был бы
        /// исполнен. Null — не решает ни один поодиночке: всё разом.
        /// </summary>
        public static string DecisiveVoice(IEnumerable<string> voices, Func<string, bool> obeysWithout)
        {
            if (voices == null || obeysWithout == null) return null;
            foreach (var id in voices)
                if (obeysWithout(id)) return id;
            return null;
        }

        /// <summary>
        /// Причина словами — в мужском роде, как все причины; род наводит
        /// <see cref="PhraseGenerator"/> на выходе. Глаголы, несущие род, —
        /// парами. Чисел нет.
        /// </summary>
        public static string Phrase(Factor f, DecisionContext c, SinType? sin, Gender gender)
        {
            string P(string he, string she) => Grammar.Pick(gender, he, she);

            switch (f)
            {
                case Factor.Distance:
                    // Как даль звучит для характера — прежние слова голоса.
                    if (sin == SinType.Pride) return "приказ крикнули издали, а он не из тех, кого зовут криком";
                    if (sin == SinType.Sloth) return P("он сделал вид, что не расслышал", "она сделала вид, что не расслышала");
                    return "Греховод был далеко — приказ еле долетел";

                case Factor.Debt:
                    if (c.UnpaidMissions > 3) return "ему не платили вылазку за вылазкой";
                    if (c.UnpaidMissions == 3) return "ему не платили третью вылазку подряд";
                    if (c.UnpaidMissions == 2) return "ему не платили вторую вылазку подряд";
                    return "ему до сих пор не заплатили";

                case Factor.Pocket:     return "ему есть что терять — карман не пустой";
                case Factor.Temptation:
                    var item = Tempting(c);
                    return item != null ? $"{item.Name.ToLowerInvariant()} тянет его сильнее приказа"
                                        : "вещь при нём тянет сильнее приказа";
                case Factor.Loot:       return "добыча лежала слишком близко";
                case Factor.Patrol:     return "ходить туда-сюда ему скучно";
                case Factor.AllyInDanger: return "рядом свой был в беде";
                case Factor.Wounds:
                    return c.MaxHP > 0f && c.CurrentHP < c.MaxHP * 0.3f
                        ? "на нём нет живого места" : "он ранен и бережёт себя";
                case Factor.Surrounded: return "его обступили со всех сторон";
                case Factor.Fatigue:
                    return c.IsExhausted ? P("он выдохся и больше не может", "она выдохлась и больше не может")
                                         : "силы у него на исходе";
                default: return "";
            }
        }

        /// <summary>Самая притягательная из вещей при нём. Нет таких — null.</summary>
        private static InventoryItem Tempting(DecisionContext c)
        {
            InventoryItem best = null;
            if (c.CarriedItems == null) return null;
            foreach (var item in c.CarriedItems)
                if (item != null && !Mathf.Approximately(item.TemptationValue, 0f)
                    && (best == null || Mathf.Abs(item.TemptationValue) > Mathf.Abs(best.TemptationValue)))
                    best = item;
            return best;
        }
    }
}
